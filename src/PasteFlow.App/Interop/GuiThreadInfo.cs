using System.Runtime.InteropServices;

namespace PasteFlow.App.Interop;

/// <summary>
/// What a user interface thread shows, including its text cursor (GUITHREADINFO).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GuiThreadInfo
{
    /// <summary>
    /// The size of the structure, in bytes, set before the call.
    /// </summary>
    public int Size;

    /// <summary>
    /// The state of the thread.
    /// </summary>
    public int Flags;

    /// <summary>
    /// The active window.
    /// </summary>
    public IntPtr Active;

    /// <summary>
    /// The window with the keyboard.
    /// </summary>
    public IntPtr Focus;

    /// <summary>
    /// The window capturing the mouse.
    /// </summary>
    public IntPtr Capture;

    /// <summary>
    /// The window owning the active menu.
    /// </summary>
    public IntPtr MenuOwner;

    /// <summary>
    /// The window being moved or sized.
    /// </summary>
    public IntPtr MoveSize;

    /// <summary>
    /// The window showing the text cursor.
    /// </summary>
    public IntPtr Caret;

    /// <summary>
    /// The text cursor, inside <see cref="Caret"/>.
    /// </summary>
    public NativeRect CaretRect;
}
