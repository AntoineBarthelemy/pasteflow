using PasteFlow.App.Interop;

namespace PasteFlow.App.Screen;

/// <summary>
/// Finds the windows of the desktop in the Alt+Tab order.
/// </summary>
internal static class DesktopWindows
{
    private const uint NextWindowCommand = 2;
    private const uint OwnerWindowCommand = 4;
    private const int ExtendedStyleIndex = -20;
    private const long ToolWindowStyle = 0x80;
    private const int CloakedAttribute = 14;
    private const int MaxScannedWindows = 500;

    /// <summary>
    /// Gets the active window followed by the application windows just behind it.
    /// </summary>
    /// <param name="count">The maximum number of windows to return, active window included.</param>
    /// <returns>The windows, active window first, then in Alt+Tab order.</returns>
    public static List<IntPtr> GetActiveAndBehind(int count)
    {
        var windows = new List<IntPtr>(count);
        var current = NativeMethods.GetForegroundWindow();
        if (current == IntPtr.Zero || count <= 0)
        {
            return windows;
        }

        windows.Add(current);
        for (var scanned = 0; scanned < MaxScannedWindows && windows.Count < count; scanned++)
        {
            current = NativeMethods.GetWindow(current, NextWindowCommand);
            if (current == IntPtr.Zero)
            {
                break;
            }

            if (IsApplicationWindow(current))
            {
                windows.Add(current);
            }
        }

        return windows;
    }

    /// <summary>
    /// Determines whether a window is a visible application window, as listed by Alt+Tab.
    /// </summary>
    /// <param name="window">The window to check.</param>
    /// <returns><see langword="true"/> for an application window; otherwise <see langword="false"/>.</returns>
    private static bool IsApplicationWindow(IntPtr window)
    {
        return NativeMethods.IsWindowVisible(window)
            && !NativeMethods.IsIconic(window)
            && NativeMethods.GetWindow(window, OwnerWindowCommand) == IntPtr.Zero
            && NativeMethods.GetWindowTextLength(window) > 0
            && (NativeMethods.GetWindowLongPtr(window, ExtendedStyleIndex) & ToolWindowStyle) == 0
            && !IsCloaked(window)
            && !BelongsToThisProcess(window);
    }

    /// <summary>
    /// Determines whether a window is hidden by Windows, for example on another virtual desktop.
    /// </summary>
    /// <param name="window">The window to check.</param>
    /// <returns><see langword="true"/> if the window is hidden; otherwise <see langword="false"/>.</returns>
    private static bool IsCloaked(IntPtr window)
    {
        return NativeMethods.DwmGetWindowAttribute(window, CloakedAttribute, out var cloaked, sizeof(int)) == 0
            && cloaked != 0;
    }

    private static bool BelongsToThisProcess(IntPtr window)
    {
        _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
        return processId == (uint)Environment.ProcessId;
    }
}
