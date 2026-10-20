using System.Windows;
using System.Windows.Controls;

namespace PasteFlow.App.Design;

/// <summary>
/// A window of PasteFlow: ivory background (or charcoal in dark mode), the fonts and controls of the theme, and a title
/// bar of the same color, so that the window reads as one calm surface.
/// </summary>
internal class ThemedWindow : Window
{
    private const double TitleBarRoom = 24;

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

        // Never taller than the screen, title bar included: on a small laptop the middle of a page scrolls instead
        MaxHeight = SystemParameters.WorkArea.Height - TitleBarRoom;
        SourceInitialized += (_, _) => Theme.PaintTitleBar(this);
    }

    /// <summary>
    /// Sets the content of the window as a page: a top and a bottom that stay in place, and a middle that scrolls when
    /// the screen is too short to show it whole.
    /// </summary>
    /// <param name="top">The top, such as the title of the window.</param>
    /// <param name="middle">The middle, with a fixed width.</param>
    /// <param name="bottom">The bottom, such as the buttons of the window.</param>
    protected void SetPage(UIElement top, FrameworkElement middle, UIElement bottom)
    {
        ArgumentNullException.ThrowIfNull(top);
        ArgumentNullException.ThrowIfNull(middle);
        ArgumentNullException.ThrowIfNull(bottom);

        var header = new Border { Padding = new Thickness(28, 24, 28, 0), Child = top };
        DockPanel.SetDock(header, Dock.Top);
        var footer = new Border { Padding = new Thickness(28, 0, 28, 22), Child = bottom };
        DockPanel.SetDock(footer, Dock.Bottom);
        middle.Margin = new Thickness(28, 0, 28, 0);
        var scroll = new ScrollViewer
        {
            Content = middle,
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var page = new DockPanel();
        page.Children.Add(header);
        page.Children.Add(footer);
        page.Children.Add(scroll);
        Content = page;
    }
}
