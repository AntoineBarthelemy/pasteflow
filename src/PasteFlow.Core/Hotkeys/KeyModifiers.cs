namespace PasteFlow.Core.Hotkeys;

/// <summary>
/// Modifier keys that can be combined to build a keyboard shortcut.
/// </summary>
/// <remarks>
/// Values match the Win32 <c>MOD_*</c> constants so they can be passed to the operating system as is.
/// </remarks>
[Flags]
public enum KeyModifiers
{
    /// <summary>
    /// No modifier key.
    /// </summary>
    None = 0,

    /// <summary>
    /// The Alt key.
    /// </summary>
    Alt = 1,

    /// <summary>
    /// The Control key.
    /// </summary>
    Control = 2,

    /// <summary>
    /// The Shift key.
    /// </summary>
    Shift = 4,

    /// <summary>
    /// The Windows logo key.
    /// </summary>
    Windows = 8,
}
