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

    /// <summary>
    /// Changes information about a window, for example its extended style with GWL_EXSTYLE (-20).
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <param name="nIndex">The information to change.</param>
    /// <param name="dwNewLong">The new value.</param>
    /// <returns>The previous value, or 0 on failure.</returns>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    /// <summary>
    /// Changes the position, size and stacking order of a window, in physical pixels.
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <param name="hWndInsertAfter">The window to place it after, for example HWND_TOPMOST (-1).</param>
    /// <param name="x">The new left side.</param>
    /// <param name="y">The new top side.</param>
    /// <param name="cx">The new width.</param>
    /// <param name="cy">The new height.</param>
    /// <param name="uFlags">Options, for example SWP_NOACTIVATE (0x10).</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>
    /// Gets the position and size of a window on the screen, in pixels.
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <param name="lpRect">The position of the window.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    /// <summary>
    /// Draws a window into a device context, even when other windows cover it.
    /// </summary>
    /// <param name="hwnd">The window.</param>
    /// <param name="hdcBlt">The device context to draw into.</param>
    /// <param name="nFlags">Options, for example PW_RENDERFULLCONTENT (2) for windows drawn by the GPU.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    /// <summary>
    /// Brings a window to the front and gives it the keyboard.
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <returns><see langword="true"/> if the window was brought to the front; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Sends keystrokes as if typed on the keyboard.
    /// </summary>
    /// <param name="cInputs">The number of keystrokes.</param>
    /// <param name="pInputs">The keystrokes.</param>
    /// <param name="cbSize">The size of one <see cref="Input"/>, in bytes.</param>
    /// <returns>The number of keystrokes sent.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint SendInput(uint cInputs, [In] Input[] pInputs, int cbSize);

    /// <summary>
    /// Tells whether a key is held down right now.
    /// </summary>
    /// <param name="vKey">The virtual-key code, for example VK_CONTROL (0x11).</param>
    /// <returns>A value whose highest bit is set while the key is down.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial short GetAsyncKeyState(int vKey);

    /// <summary>
    /// Gets where the text cursor of a thread is, for applications that show a classic Windows caret.
    /// </summary>
    /// <param name="idThread">The thread, or 0 for the thread of the foreground window.</param>
    /// <param name="pgui">The information, with its size set beforehand.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo pgui);

    /// <summary>
    /// Converts a point of the inside of a window to screen pixels.
    /// </summary>
    /// <param name="hWnd">The window.</param>
    /// <param name="lpPoint">The point, converted in place.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ClientToScreen(IntPtr hWnd, ref NativePoint lpPoint);

    /// <summary>
    /// Gets the position of the mouse pointer in screen pixels.
    /// </summary>
    /// <param name="lpPoint">The position.</param>
    /// <returns><see langword="true"/> on success; otherwise <see langword="false"/>.</returns>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out NativePoint lpPoint);
}
