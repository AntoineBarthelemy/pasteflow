using System.Windows;
using PasteFlow.App.Interop;

namespace PasteFlow.App.Highlights;

/// <summary>
/// Turns windows into overlays drawn above other applications, and places them in screen pixels.
/// </summary>
internal static class OverlayWindows
{
    /// <summary>
    /// The extended style that lets clicks go through a window.
    /// </summary>
    internal const nint WsExTransparent = 0x20;

    /// <summary>
    /// The extended style that keeps a window from ever taking the focus.
    /// </summary>
    internal const nint WsExNoActivate = 0x08000000;

    private const int GwlExStyle = -20;
    private const nint WsExToolWindow = 0x80;
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoActivate = 0x10;

    private static readonly IntPtr HwndTopmost = new(-1);

    /// <summary>
    /// Keeps a window from taking the focus and from appearing in Alt+Tab, and optionally lets clicks go through it.
    /// </summary>
    /// <param name="handle">The window.</param>
    /// <param name="letClicksThrough">Whether clicks go through the window to the application below.</param>
    public static void MakeOverlay(IntPtr handle, bool letClicksThrough)
    {
        var style = NativeMethods.GetWindowLongPtr(handle, GwlExStyle) | WsExToolWindow | WsExNoActivate;
        if (letClicksThrough)
        {
            style |= WsExTransparent;
        }

        _ = NativeMethods.SetWindowLongPtr(handle, GwlExStyle, style);
    }

    /// <summary>
    /// Places and sizes a window above the others, in screen pixels as given by UI Automation.
    /// </summary>
    /// <param name="handle">The window.</param>
    /// <param name="bounds">The new position and size.</param>
    public static void Place(IntPtr handle, Rect bounds)
    {
        _ = NativeMethods.SetWindowPos(
            handle,
            HwndTopmost,
            (int)Math.Round(bounds.X),
            (int)Math.Round(bounds.Y),
            (int)Math.Round(bounds.Width),
            (int)Math.Round(bounds.Height),
            SwpNoActivate);
    }

    /// <summary>
    /// Moves a window above the others without changing its size, in screen pixels.
    /// </summary>
    /// <param name="handle">The window.</param>
    /// <param name="position">The new top left corner.</param>
    public static void Move(IntPtr handle, Point position)
    {
        _ = NativeMethods.SetWindowPos(
            handle,
            HwndTopmost,
            (int)Math.Round(position.X),
            (int)Math.Round(position.Y),
            0,
            0,
            SwpNoActivate | SwpNoSize);
    }
}
