using System.Runtime.InteropServices;

namespace PasteFlow.App.Interop;

/// <summary>
/// Win32 functions used by the application.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    /// Defines a system-wide shortcut.
    /// </summary>
    /// <param name="hWnd">The window that receives the WM_HOTKEY messages.</param>
    /// <param name="id">The identifier of the shortcut within the window.</param>
    /// <param name="fsModifiers">The modifier keys, combined with MOD_NOREPEAT if needed.</param>
    /// <param name="vk">The virtual-key code of the main key.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> if the shortcut is already in use.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>
    /// Frees a system-wide shortcut previously defined by <see cref="RegisterHotKey"/>.
    /// </summary>
    /// <param name="hWnd">The window associated with the shortcut.</param>
    /// <param name="id">The identifier of the shortcut within the window.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Gets the window the user is currently working with.
    /// </summary>
    /// <returns>The handle of the foreground window, or <see cref="IntPtr.Zero"/> if there is none.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial IntPtr GetForegroundWindow();

    /// <summary>
    /// Gets a window related to another one, for example the next one in the Alt+Tab order.
    /// </summary>
    /// <param name="hWnd">The reference window.</param>
    /// <param name="uCmd">The relationship, for example GW_HWNDNEXT (2) or GW_OWNER (4).</param>
    /// <returns>The related window, or <see cref="IntPtr.Zero"/> if there is none.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    /// <summary>
    /// Determines whether a window is visible.
    /// </summary>
    /// <param name="hWnd">The window to check.</param>
    /// <returns><see langword="true"/> if the window has the visible style; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// Determines whether a window is minimized.
    /// </summary>
    /// <param name="hWnd">The window to check.</param>
    /// <returns><see langword="true"/> if the window is minimized; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(IntPtr hWnd);

    /// <summary>
    /// Gets the length of the title of a window.
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <returns>The number of characters of the title, 0 if the window has no title.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// Gets information about a window, for example its extended style with GWL_EXSTYLE (-20).
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <param name="nIndex">The information to get.</param>
    /// <returns>The requested value.</returns>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    /// <summary>
    /// Gets the process that created a window.
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <param name="lpdwProcessId">The identifier of the process.</param>
    /// <returns>The identifier of the thread that created the window.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Gets an attribute of a window from the desktop window manager, for example DWMWA_CLOAKED (14).
    /// </summary>
    /// <param name="hwnd">The window.</param>
    /// <param name="dwAttribute">The attribute to get.</param>
    /// <param name="pvAttribute">The value of the attribute.</param>
    /// <param name="cbAttribute">The size of the value, in bytes.</param>
    /// <returns>0 on success; otherwise an error code.</returns>
    [LibraryImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
}
