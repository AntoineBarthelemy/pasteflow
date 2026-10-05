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
}
