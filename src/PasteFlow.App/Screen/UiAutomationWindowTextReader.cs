using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads the text of the active window and of the windows just behind it through UI Automation.
/// </summary>
/// <remarks>
/// <para>Windows are read in parallel; a window that does not answer in time is skipped so the whole reading stays fast.</para>
/// <para>A window behind the form that gives little or no text, such as a picture, a scanned PDF or a remote desktop,
/// is read from its image with the character recognition of Windows (OCR), when available and allowed.</para>
/// </remarks>
internal sealed class UiAutomationWindowTextReader : IWindowTextReader
{
    /// <summary>
    /// Below this number of letters and digits, a window is considered to give no text.
    /// </summary>
    internal const int MinAccessibleTextLength = 30;

    private const int MaxWindows = 4;
    private const int MaxDocumentLength = 100_000;
    private const string UntitledWindow = "Fenêtre sans texte";

    private static readonly TimeSpan WindowTimeout = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan ImageReadingTimeout = TimeSpan.FromMilliseconds(1500);

    private static readonly Condition DocumentCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);

    private static readonly Condition TextCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);

    private readonly OcrTextRecognizer? _ocr;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationWindowTextReader"/> class that reads accessible text only.
    /// </summary>
    public UiAutomationWindowTextReader()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UiAutomationWindowTextReader"/> class that reads the image of
    /// windows giving no text.
    /// </summary>
    /// <param name="ocr">The character recognition, or <see langword="null"/> when Windows has none.</param>
    public UiAutomationWindowTextReader(OcrTextRecognizer? ocr)
    {
        _ocr = ocr;
    }

    /// <summary>
    /// Gets or sets a value indicating whether windows giving no text are read from their image.
    /// </summary>
    public bool ReadImages { get; set; } = true;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
    {
        var windows = DesktopWindows.GetActiveAndBehind(MaxWindows);
        var readings = windows.Select((window, rank) => ReadWithFallbackAsync(window, rank, cancellationToken));

        var results = await Task.WhenAll(readings).ConfigureAwait(false);
        return results.OfType<WindowText>().ToList();
    }

    /// <summary>
    /// Reads the text of one window.
    /// </summary>
    /// <param name="handle">The window to read.</param>
    /// <param name="rank">The position of the window in the Alt+Tab order.</param>
    /// <returns>The text of the window, or <see langword="null"/> if the window cannot be read.</returns>
    internal static WindowText? ReadWindow(IntPtr handle, int rank)
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
    /// Reads the text of one window, from its image when it gives too little text.
    /// </summary>
    /// <param name="handle">The window to read.</param>
    /// <param name="rank">The position of the window in the Alt+Tab order; 0 is the form itself, never read from its image.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The text of the window, or <see langword="null"/> if the window cannot be read.</returns>
    internal async Task<WindowText?> ReadWithFallbackAsync(IntPtr handle, int rank, CancellationToken cancellationToken)
    {
        var accessible = await ReadWithTimeoutAsync(handle, rank, cancellationToken).ConfigureAwait(false);
        var letterCount = accessible?.Text.Count(char.IsLetterOrDigit) ?? 0;
        if (rank == 0 || _ocr is null || !ReadImages || letterCount >= MinAccessibleTextLength)
        {
            return accessible;
        }

        var fromImage = await ReadImageWithTimeoutAsync(_ocr, handle, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(fromImage) || fromImage.Count(char.IsLetterOrDigit) <= letterCount)
        {
            return accessible;
        }

        var title = accessible is { Title: { } accessibleTitle } && !string.IsNullOrWhiteSpace(accessibleTitle)
            ? accessibleTitle
            : UntitledWindow;
        return new WindowText { Title = title, Text = fromImage, Rank = rank, IsReadFromImage = true };
    }

    private static async Task<WindowText?> ReadWithTimeoutAsync(IntPtr window, int rank, CancellationToken cancellationToken)
    {
        var reading = Task.Run(() => ReadWindow(window, rank), cancellationToken);
        var finished = await Task.WhenAny(reading, Task.Delay(WindowTimeout, cancellationToken)).ConfigureAwait(false);

        // A late window is ignored: UI Automation calls cannot be interrupted, the reading ends in the background
        return finished == reading ? await reading.ConfigureAwait(false) : null;
    }

    private static async Task<string?> ReadImageWithTimeoutAsync(OcrTextRecognizer ocr, IntPtr handle, CancellationToken cancellationToken)
    {
        try
        {
            var reading = Task.Run(
                async () =>
                {
                    var png = WindowCapture.TryCapturePng(handle);
                    return png is null ? null : await ocr.RecognizeAsync(png, cancellationToken).ConfigureAwait(false);
                },
                cancellationToken);
            var finished = await Task.WhenAny(reading, Task.Delay(ImageReadingTimeout, cancellationToken)).ConfigureAwait(false);
            return finished == reading ? await reading.ConfigureAwait(false) : null;
        }
        catch (Exception exception) when (exception is ExternalException or ArgumentException)
        {
            // The picture could not be taken or read: the window simply gives no text
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
