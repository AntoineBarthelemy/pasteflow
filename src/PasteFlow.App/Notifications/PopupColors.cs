using System.Windows.Media;
using PasteFlow.App.Design;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Colors of the popups, taken from the palette of the theme when a popup is built.
/// </summary>
internal static class PopupColors
{
    /// <summary>
    /// Gets the background of the popups.
    /// </summary>
    public static Brush Background => Theme.Brush(palette => palette.Surface);

    /// <summary>
    /// Gets the color of the main text.
    /// </summary>
    public static Brush Text => Theme.Brush(palette => palette.Text);

    /// <summary>
    /// Gets the color of the labels.
    /// </summary>
    public static Brush SecondaryText => Theme.Brush(palette => palette.TextSecondary);

    /// <summary>
    /// Gets the color of the hints and sources.
    /// </summary>
    public static Brush DiscreetText => Theme.Brush(palette => palette.TextTertiary);

    /// <summary>
    /// Gets the color of the separations.
    /// </summary>
    public static Brush Separator => Theme.Brush(palette => palette.Border);

    /// <summary>
    /// Gets the color of a field filled.
    /// </summary>
    public static Brush Filled => Theme.Brush(palette => palette.Success);

    /// <summary>
    /// Gets the color of a field to check.
    /// </summary>
    public static Brush ToCheck => Theme.Brush(palette => palette.Warning);

    /// <summary>
    /// Gets the color of a field that could not be written.
    /// </summary>
    public static Brush Failed => Theme.Brush(palette => palette.Danger);
}
