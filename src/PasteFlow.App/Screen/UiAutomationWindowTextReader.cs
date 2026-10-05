using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads the text of the active window and of the windows just behind it through UI Automation.
/// </summary>
/// <remarks>
/// Windows are read in parallel; a window that does not answer in time is skipped so the whole reading stays fast.
/// </remarks>
internal sealed class UiAutomationWindowTextReader : IWindowTextReader
{
    private const int MaxWindows = 4;
    private const int MaxDocumentLength = 100_000;

    private static readonly TimeSpan WindowTimeout = TimeSpan.FromMilliseconds(600);

    private static readonly Condition DocumentCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);

    private static readonly Condition TextCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
    {
        var windows = DesktopWindows.GetActiveAndBehind(MaxWindows);
        var readings = windows.Select((window, rank) => ReadWithTimeoutAsync(window, rank, cancellationToken));

        var results = await Task.WhenAll(readings).ConfigureAwait(false);
        return results.OfType<WindowText>().ToList();
    }

    private static async Task<WindowText?> ReadWithTimeoutAsync(IntPtr window, int rank, CancellationToken cancellationToken)
    {
        var reading = Task.Run(() => ReadWindow(window, rank), cancellationToken);
        var finished = await Task.WhenAny(reading, Task.Delay(WindowTimeout, cancellationToken)).ConfigureAwait(false);

        // A late window is ignored: UI Automation calls cannot be interrupted, the reading ends in the background
        return finished == reading ? await reading.ConfigureAwait(false) : null;
    }

    private static WindowText? ReadWindow(IntPtr handle, int rank)
    {
        try
        {
            var window = AutomationElement.FromHandle(handle);
            var text = ReadDocuments(window) ?? ReadTextElements(window);
            return new WindowText { Title = window.Current.Name, Text = text, Rank = rank };
        }
        catch (ElementNotAvailableException)
        {
            // The window was closed while being read
            return null;
        }
        catch (COMException)
        {
            // The application does not answer accessibility requests
            return null;
        }
    }

    /// <summary>
    /// Reads documents such as web pages or text editors, which expose their whole text at once.
    /// </summary>
    /// <param name="window">The window to read.</param>
    /// <returns>The text of the documents, or <see langword="null"/> if the window has none.</returns>
    private static string? ReadDocuments(AutomationElement window)
    {
        var builder = new StringBuilder();
        foreach (AutomationElement document in window.FindAll(TreeScope.Descendants, DocumentCondition))
        {
            if (document.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern textPattern)
            {
                builder.AppendLine(textPattern.DocumentRange.GetText(MaxDocumentLength));
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    /// <summary>
    /// Reads the text labels of classic applications, one per line.
    /// </summary>
    /// <param name="window">The window to read.</param>
    /// <returns>The labels separated by line breaks.</returns>
    private static string ReadTextElements(AutomationElement window)
    {
        var labels = window.FindAll(TreeScope.Descendants, TextCondition)
            .Cast<AutomationElement>()
            .Select(element => element.Current.Name);

        return string.Join('\n', labels);
    }
}
