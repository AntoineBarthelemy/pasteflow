using System.Windows.Media;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Colors of the popups, on a dark background.
/// </summary>
internal static class PopupColors
{
    /// <summary>
    /// Gets the background of the popups.
    /// </summary>
    public static Brush Background { get; } = Create(0x20, 0x20, 0x20);

    /// <summary>
    /// Gets the color of the main text.
    /// </summary>
    public static Brush Text { get; } = Brushes.White;

    /// <summary>
    /// Gets the color of secondary text, such as labels.
    /// </summary>
    public static Brush SecondaryText { get; } = Create(0xB4, 0xB4, 0xB4);

    /// <summary>
    /// Gets the color of discreet text, such as details.
    /// </summary>
    public static Brush DiscreetText { get; } = Create(0x8C, 0x8C, 0x8C);

    /// <summary>
    /// Gets the color of separators.
    /// </summary>
    public static Brush Separator { get; } = Create(0x3A, 0x3A, 0x3A);

    /// <summary>
    /// Gets the color of fields filled.
    /// </summary>
    public static Brush Filled { get; } = Create(0x22, 0xC5, 0x5E);

    /// <summary>
    /// Gets the color of fields to check, the same as their frame on screen.
    /// </summary>
    public static Brush ToCheck { get; } = Create(0xF5, 0x9E, 0x0B);

    /// <summary>
    /// Gets the color of fields that refused their value.
    /// </summary>
    public static Brush Failed { get; } = Create(0xEF, 0x44, 0x44);

    private static SolidColorBrush Create(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
