using System.Runtime.InteropServices;
using System.Windows.Automation;
using PasteFlow.App.Interop;
using PasteFlow.Core.Hints;
using Rect = System.Windows.Rect;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads the texts really seen on screen, each with its place, in the window in front and the windows behind it:
/// the values the letters of <see cref="HintCodes"/> are put on.
/// </summary>
/// <remarks>
/// <para>A text hidden by another window, scrolled out of sight or outside the screen is left aside: only what the
/// user sees gets letters.</para>
/// <para>Password fields are never read.</para>
/// </remarks>
internal static class VisibleTextReader
{
    private const int MaxWindows = 4;
    private const int MaxElementsPerWindow = 1500;
    private const int MaxTextLength = 400;
    private const uint RootAncestor = 2;

    private static readonly TimeSpan WindowTimeout = TimeSpan.FromMilliseconds(1200);

    private static readonly Condition ReadableCondition = new OrCondition(
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem));

    /// <summary>
    /// Reads the texts seen on screen.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The texts with their places in screen pixels; a window too slow to answer is left aside.</returns>
    public static async Task<IReadOnlyList<ScreenText>> ReadAsync(CancellationToken cancellationToken)
    {
        var windows = DesktopWindows.GetActiveAndBehind(MaxWindows);
        var readings = await Task.WhenAll(windows.Select(window => ReadWithTimeoutAsync(window, cancellationToken))).ConfigureAwait(false);
        return readings.SelectMany(texts => texts).ToList();
    }

    /// <summary>
    /// Reads the texts seen of one window.
    /// </summary>
    /// <param name="handle">The top-level window.</param>
    /// <returns>The texts with their places in screen pixels.</returns>
    internal static List<ScreenText> ReadWindow(IntPtr handle)
    {
        var texts = new List<ScreenText>();
        try
        {
            // The elements themselves are kept with their values: without them, UI Automation fails to return the list
            var request = new CacheRequest();
            request.Add(AutomationElement.NameProperty);
            request.Add(AutomationElement.BoundingRectangleProperty);
            request.Add(AutomationElement.IsOffscreenProperty);
            request.Add(AutomationElement.ControlTypeProperty);
            request.Add(AutomationElement.IsPasswordProperty);
            request.Add(ValuePattern.ValueProperty);

            AutomationElementCollection elements;
            using (request.Activate())
            {
                elements = AutomationElement.FromHandle(handle).FindAll(TreeScope.Descendants, ReadableCondition);
            }

            foreach (AutomationElement element in elements)
            {
                if (texts.Count == MaxElementsPerWindow)
                {
                    break;
                }

                try
                {
                    if (TryRead(element, handle) is { } text)
                    {
                        texts.Add(text);
                    }
                }
                catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException or ArgumentException or COMException)
                {
                    // One element that answers badly does not stop the others
                }
            }
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            // The window was closed or does not answer accessibility requests: what was read is kept
        }

        return texts;
    }

    private static ScreenText? TryRead(AutomationElement element, IntPtr window)
    {
        // Some applications answer oddly typed values: anything unexpected leaves the element aside
        if (element.GetCachedPropertyValue(AutomationElement.BoundingRectangleProperty) is not Rect bounds
            || element.GetCachedPropertyValue(AutomationElement.IsOffscreenProperty) is not false
            || bounds.IsEmpty || bounds.Width < 1 || bounds.Height < 1)
        {
            return null;
        }

        // A field shows its value, its name is its label; a password is never read
        var isField = element.GetCachedPropertyValue(AutomationElement.ControlTypeProperty) is ControlType type && type == ControlType.Edit;
        if (isField && element.GetCachedPropertyValue(AutomationElement.IsPasswordProperty) is not false)
        {
            return null;
        }

        var text = isField
            ? element.GetCachedPropertyValue(ValuePattern.ValueProperty, ignoreDefaultValue: true) as string
            : element.GetCachedPropertyValue(AutomationElement.NameProperty) as string;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength || !IsSeen(bounds, window))
        {
            return null;
        }

        return new ScreenText { Text = text, Left = bounds.Left, Top = bounds.Top, Width = bounds.Width, Height = bounds.Height };
    }

    /// <summary>
    /// Tells whether the user sees a text: the middle of its first line belongs to its window, not to a window above.
    /// </summary>
    /// <param name="bounds">The place of the text, in screen pixels.</param>
    /// <param name="window">The top-level window of the text.</param>
    /// <returns><see langword="true"/> if the text is seen; otherwise <see langword="false"/>.</returns>
    private static bool IsSeen(Rect bounds, IntPtr window)
    {
        var point = new NativePoint((int)Math.Round(bounds.Left + Math.Min(bounds.Width / 2, 20)), (int)Math.Round(bounds.Top + Math.Min(bounds.Height / 2, 8)));
        var seen = NativeMethods.WindowFromPoint(point);
        return seen != IntPtr.Zero && NativeMethods.GetAncestor(seen, RootAncestor) == window;
    }

    private static async Task<List<ScreenText>> ReadWithTimeoutAsync(IntPtr window, CancellationToken cancellationToken)
    {
        var reading = Task.Run(() => ReadWindow(window), cancellationToken);
        var finished = await Task.WhenAny(reading, Task.Delay(WindowTimeout, cancellationToken)).ConfigureAwait(false);

        // A late window is left aside: UI Automation calls cannot be interrupted, the reading ends in the background
        return finished == reading ? await reading.ConfigureAwait(false) : new List<ScreenText>();
    }
}
