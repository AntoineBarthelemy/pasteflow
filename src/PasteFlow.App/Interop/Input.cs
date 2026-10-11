using System.Runtime.InteropServices;

namespace PasteFlow.App.Interop;

/// <summary>
/// One keystroke or mouse event sent by <see cref="NativeMethods.SendInput"/> (INPUT).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    /// <summary>
    /// The kind of event: 1 for the keyboard.
    /// </summary>
    public uint Type;

    /// <summary>
    /// The event itself.
    /// </summary>
    public InputUnion Union;
}

/// <summary>
/// The event of an <see cref="Input"/>: the mouse part only gives the structure its full size.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    /// <summary>
    /// A mouse event.
    /// </summary>
    [FieldOffset(0)]
    public MouseInput Mouse;

    /// <summary>
    /// A keystroke.
    /// </summary>
    [FieldOffset(0)]
    public KeyboardInput Keyboard;
}

/// <summary>
/// A mouse event (MOUSEINPUT).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    /// <summary>
    /// The horizontal move.
    /// </summary>
    public int X;

    /// <summary>
    /// The vertical move.
    /// </summary>
    public int Y;

    /// <summary>
    /// The wheel move.
    /// </summary>
    public uint Data;

    /// <summary>
    /// What happened.
    /// </summary>
    public uint Flags;

    /// <summary>
    /// When it happened, 0 for now.
    /// </summary>
    public uint Time;

    /// <summary>
    /// Extra information.
    /// </summary>
    public nint ExtraInfo;
}

/// <summary>
/// A keystroke (KEYBDINPUT).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    /// <summary>
    /// The virtual-key code, 0 for a character.
    /// </summary>
    public ushort VirtualKey;

    /// <summary>
    /// The character, with KEYEVENTF_UNICODE.
    /// </summary>
    public ushort Scan;

    /// <summary>
    /// KEYEVENTF_KEYUP (2), KEYEVENTF_UNICODE (4).
    /// </summary>
    public uint Flags;

    /// <summary>
    /// When it happened, 0 for now.
    /// </summary>
    public uint Time;

    /// <summary>
    /// Extra information.
    /// </summary>
    public nint ExtraInfo;
}
