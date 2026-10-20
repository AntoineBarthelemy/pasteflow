using System.Windows;

namespace PasteFlow.App.Design;

/// <summary>
/// A window of PasteFlow: ivory background (or charcoal in dark mode), the fonts and controls of the theme, and a title
/// bar of the same color, so that the window reads as one calm surface.
/// </summary>
internal class ThemedWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ThemedWindow"/> class.
    /// </summary>
    public ThemedWindow()
    {
        Theme.Apply(this);
        Background = Theme.Brush(palette => palette.Background);
        Foreground = Theme.Brush(palette => palette.Text);
        FontFamily = Theme.UiFont;
        FontSize = 13;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SourceInitialized += (_, _) => Theme.PaintTitleBar(this);
    }
}
