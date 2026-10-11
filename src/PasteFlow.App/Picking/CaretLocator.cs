using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using PasteFlow.App.Interop;

namespace PasteFlow.App.Picking;

/// <summary>
/// Finds where the text cursor is on the screen, to show a list right below it.
/// </summary>
internal static class CaretLocator
{
    /// <summary>
    /// Finds the text cursor of the active window, in screen pixels: through UI Automation, else the classic Windows
    /// caret, else the element holding the keyboard, else the mouse pointer.
    /// </summary>
    /// <returns>The place of the cursor.</returns>
    public static Rect Locate()
    {
        return FromAutomation() ?? FromWindowsCaret() ?? FromMouse();
    }

    private static Rect? FromAutomation()
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is null)
            {
                return null;
            }

            if (focused.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern text
                && text.GetSelection() is { Length: > 0 } selection)
            {
                var caret = selection[0].Clone();
                caret.ExpandToEnclosingUnit(TextUnit.Character);
                var rectangles = caret.GetBoundingRectangles();
                if (rectangles.Length > 0 && !rectangles[0].IsEmpty && rectangles[0].Width >= 0)
                {
                    return rectangles[0];
                }
            }

            var bounds = focused.Current.BoundingRectangle;

            // A whole document is too big to be the cursor: the classic caret may be more precise
            return bounds.IsEmpty || bounds.Height > SystemParameters.PrimaryScreenHeight / 3 ? null : bounds;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            return null;
        }
    }

    private static Rect? FromWindowsCaret()
    {
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (!NativeMethods.GetGUIThreadInfo(0, ref info) || info.Caret == IntPtr.Zero)
        {
            return null;
        }

        var topLeft = new NativePoint(info.CaretRect.Left, info.CaretRect.Top);
        return NativeMethods.ClientToScreen(info.Caret, ref topLeft)
            ? new Rect(topLeft.X, topLeft.Y, Math.Max(info.CaretRect.Width, 1), Math.Max(info.CaretRect.Height, 1))
            : null;
    }

    private static Rect FromMouse()
    {
        return NativeMethods.GetCursorPos(out var point) ? new Rect(point.X, point.Y, 1, 16) : new Rect(100, 100, 1, 16);
    }
}
