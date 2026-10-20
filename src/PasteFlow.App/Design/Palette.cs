using System.Windows.Media;

namespace PasteFlow.App.Design;

/// <summary>
/// The colors of PasteFlow: warm neutrals and a terracotta accent, in a light and a dark version that follow the theme
/// of Windows.
/// </summary>
/// <param name="Background">The background of the windows.</param>
/// <param name="Surface">Cards, popups and fields.</param>
/// <param name="Sunken">Areas set back, such as a search box or a hovered row.</param>
/// <param name="Border">Fine separations.</param>
/// <param name="BorderStrong">The outline of fields and buttons.</param>
/// <param name="Text">The main text.</param>
/// <param name="TextSecondary">Labels and descriptions.</param>
/// <param name="TextTertiary">Hints and sources.</param>
/// <param name="Accent">The color of PasteFlow: main actions, selection, focus.</param>
/// <param name="AccentHover">The accent under the mouse.</param>
/// <param name="AccentSoft">A light wash of the accent, behind a selected row.</param>
/// <param name="OnAccent">Text on the accent.</param>
/// <param name="Success">A field filled.</param>
/// <param name="Warning">A field to check.</param>
/// <param name="WarningSoft">A light wash of the warning color.</param>
/// <param name="Danger">An error.</param>
/// <param name="Shadow">The color of the shadows.</param>
/// <param name="IsDark">Whether the palette is the dark one.</param>
internal sealed record Palette(
    Color Background,
    Color Surface,
    Color Sunken,
    Color Border,
    Color BorderStrong,
    Color Text,
    Color TextSecondary,
    Color TextTertiary,
    Color Accent,
    Color AccentHover,
    Color AccentSoft,
    Color OnAccent,
    Color Success,
    Color Warning,
    Color WarningSoft,
    Color Danger,
    Color Shadow,
    bool IsDark)
{
    /// <summary>
    /// Gets the light palette: ivory background, white cards, ink text.
    /// </summary>
    public static Palette Light { get; } = new(
        Background: Rgb(0xFA, 0xF9, 0xF5),
        Surface: Rgb(0xFF, 0xFF, 0xFF),
        Sunken: Rgb(0xF1, 0xEF, 0xE8),
        Border: Rgb(0xEA, 0xE7, 0xDE),
        BorderStrong: Rgb(0xD6, 0xD2, 0xC6),
        Text: Rgb(0x1F, 0x1E, 0x1C),
        TextSecondary: Rgb(0x5C, 0x5A, 0x54),
        TextTertiary: Rgb(0x8C, 0x89, 0x80),
        Accent: Rgb(0xC9, 0x64, 0x42),
        AccentHover: Rgb(0xB5, 0x56, 0x37),
        AccentSoft: Rgb(0xF6, 0xE7, 0xE0),
        OnAccent: Rgb(0xFF, 0xFF, 0xFF),
        Success: Rgb(0x3E, 0x7C, 0x55),
        Warning: Rgb(0xC9, 0x64, 0x42),
        WarningSoft: Rgb(0xF6, 0xE7, 0xE0),
        Danger: Rgb(0xB4, 0x3C, 0x2E),
        Shadow: Color.FromArgb(0x26, 0x29, 0x24, 0x1C),
        IsDark: false);

    /// <summary>
    /// Gets the dark palette: warm charcoal background, raised cards, ivory text.
    /// </summary>
    public static Palette Dark { get; } = new(
        Background: Rgb(0x26, 0x26, 0x24),
        Surface: Rgb(0x30, 0x30, 0x2E),
        Sunken: Rgb(0x1F, 0x1F, 0x1D),
        Border: Rgb(0x3D, 0x3D, 0x3A),
        BorderStrong: Rgb(0x4E, 0x4D, 0x49),
        Text: Rgb(0xF2, 0xF0, 0xEA),
        TextSecondary: Rgb(0xB9, 0xB6, 0xAD),
        TextTertiary: Rgb(0x8F, 0x8C, 0x84),
        Accent: Rgb(0xD9, 0x77, 0x57),
        AccentHover: Rgb(0xE4, 0x8B, 0x6D),
        AccentSoft: Rgb(0x45, 0x30, 0x28),
        OnAccent: Rgb(0xFF, 0xFF, 0xFF),
        Success: Rgb(0x7F, 0xBF, 0x8E),
        Warning: Rgb(0xE0, 0x8A, 0x6A),
        WarningSoft: Rgb(0x45, 0x30, 0x28),
        Danger: Rgb(0xE5, 0x7F, 0x6C),
        Shadow: Color.FromArgb(0x66, 0x00, 0x00, 0x00),
        IsDark: true);

    private static Color Rgb(byte red, byte green, byte blue)
    {
        return Color.FromRgb(red, green, blue);
    }
}
