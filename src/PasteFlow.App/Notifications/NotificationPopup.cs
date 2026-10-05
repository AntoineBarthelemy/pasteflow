using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Small borderless window that shows a message in the bottom right corner of the screen without taking the focus.
/// </summary>
/// <remarks>
/// Unlike Windows notifications, it is displayed even when "Do not disturb" is on.
/// </remarks>
internal sealed class NotificationPopup : Window
{
    private const double ScreenMargin = 16;
    private const double MaxTextWidth = 360;

    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(2.5);

    private readonly DispatcherTimer _closeTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationPopup"/> class.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public NotificationPopup(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 14,
                MaxWidth = MaxTextWidth,
                TextWrapping = TextWrapping.Wrap,
            },
        };

        _closeTimer = new DispatcherTimer { Interval = DisplayDuration };
        _closeTimer.Tick += OnCloseTimerTick;
        Loaded += OnLoaded;
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        _closeTimer.Stop();
        base.OnClosed(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - ScreenMargin;
        Top = workArea.Bottom - ActualHeight - ScreenMargin;
        _closeTimer.Start();
    }

    private void OnCloseTimerTick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        Close();
    }
}
