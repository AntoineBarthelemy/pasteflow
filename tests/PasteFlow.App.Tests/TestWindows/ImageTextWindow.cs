using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PasteFlow.App.Tests.TestWindows;

/// <summary>
/// Window showing text only as a picture, as a scanned document or a remote desktop does: it gives no text to the
/// accessibility API.
/// </summary>
internal sealed class ImageTextWindow : Window
{
    public const string OrderNumber = "CMD-48213";

    public ImageTextWindow()
    {
        Title = "Document scanné";
        Width = 700;
        Height = 260;
        Background = Brushes.White;

        var text = new TextBlock
        {
            Text = $"Commande {OrderNumber}\nLivraison le 14/10/2026",
            FontSize = 36,
            FontFamily = new FontFamily("Arial"),
            Foreground = Brushes.Black,
            Background = Brushes.White,
            Padding = new Thickness(20),
        };
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        text.Arrange(new Rect(text.DesiredSize));

        var picture = new RenderTargetBitmap((int)Math.Ceiling(text.ActualWidth), (int)Math.Ceiling(text.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        picture.Render(text);
        Content = new Image { Source = picture, Stretch = Stretch.None, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Drawn by the graphics card, the window sometimes gives an empty picture to its capture on a busy machine,
        // after many browser windows: drawn by the processor, it always shows the text the tests read
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
        }
    }
}
