using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace PasteFlow.App.Highlights;

/// <summary>
/// Orange frame drawn around a field of another application, above it, without taking the focus or the clicks.
/// </summary>
internal sealed class FieldFrame : Window
{
    /// <summary>
    /// The space between the field and its frame, in pixels.
    /// </summary>
    internal const int FrameMargin = 3;

    private const double HiddenPosition = -32000;

    private static readonly Brush FrameBrush = CreateFrameBrush();

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldFrame"/> class, not shown yet.
    /// </summary>
    public FieldFrame()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;

        // Created out of sight: the real position is given in pixels once the window exists
        Left = HiddenPosition;
        Top = HiddenPosition;
        Width = 1;
        Height = 1;

        Content = new Border
        {
            BorderBrush = FrameBrush,
            BorderThickness = new Thickness(FrameMargin),
            CornerRadius = new CornerRadius(4),
        };

        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// Gets the position of the frame in screen pixels, empty while it is hidden.
    /// </summary>
    internal Rect PixelBounds { get; private set; } = Rect.Empty;

    /// <summary>
    /// Gets the handle of the window of the frame.
    /// </summary>
    internal IntPtr Handle => new WindowInteropHelper(this).Handle;

    /// <summary>
    /// Shows the frame around a field, or hides it when the field is not visible.
    /// </summary>
    /// <param name="fieldBounds">The position of the field in screen pixels, empty when it is not visible.</param>
    public void Surround(Rect fieldBounds)
    {
        if (fieldBounds.IsEmpty || fieldBounds.Width <= 0 || fieldBounds.Height <= 0)
        {
            Hide();
            PixelBounds = Rect.Empty;
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        var bounds = fieldBounds;
        bounds.Inflate(FrameMargin, FrameMargin);
        if (bounds == PixelBounds)
        {
            return;
        }

        // Pixels, as given by UI Automation: no conversion to WPF units is needed
        OverlayWindows.Place(Handle, bounds);
        PixelBounds = bounds;
    }

    private static SolidColorBrush CreateFrameBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
        brush.Freeze();
        return brush;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Clicks go through the frame to the field, and the frame never takes the focus nor appears in Alt+Tab
        OverlayWindows.MakeOverlay(Handle, letClicksThrough: true);
    }
}
