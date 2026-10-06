using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Small borderless window that shows a message or a recap in the bottom right corner of the screen without
/// taking the focus.
/// </summary>
/// <remarks>
/// Unlike Windows notifications, it is displayed even when "Do not disturb" is on.
/// It stays open while the mouse is over it, to leave time to read it.
/// </remarks>
internal sealed class NotificationPopup : Window
{
    private const double ScreenMargin = 16;
    private const double MaxTextWidth = 360;

    // Display time grows with the length of the message, to leave time to read it
    private const int MinDisplayMilliseconds = 2500;
    private const int MaxDisplayMilliseconds = 8000;
    private const int DisplayMillisecondsPerCharacter = 40;

    private readonly DispatcherTimer _closeTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationPopup"/> class showing a text message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public NotificationPopup(string message)
        : this(CreateMessageContent(message), DisplayTimeFor(message))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationPopup"/> class showing any content.
    /// </summary>
    /// <param name="content">The content to display.</param>
    /// <param name="displayTime">How long the popup stays open when the mouse is not over it.</param>
    public NotificationPopup(UIElement content, TimeSpan displayTime)
    {
        ArgumentNullException.ThrowIfNull(content);

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
            Background = PopupColors.Background,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Child = content,
        };

        DisplayTime = displayTime;
        _closeTimer = new DispatcherTimer { Interval = displayTime };
        _closeTimer.Tick += OnCloseTimerTick;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Gets how long the popup stays open when the mouse is not over it.
    /// </summary>
    internal TimeSpan DisplayTime { get; }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        _closeTimer.Stop();
        base.OnClosed(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        _closeTimer.Stop();
        base.OnMouseEnter(e);
    }

    /// <inheritdoc/>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _closeTimer.Start();
        base.OnMouseLeave(e);
    }

    private static TextBlock CreateMessageContent(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new TextBlock
        {
            Text = message,
            Foreground = PopupColors.Text,
            FontSize = 14,
            MaxWidth = MaxTextWidth,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private static TimeSpan DisplayTimeFor(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return TimeSpan.FromMilliseconds(Math.Clamp(
            MinDisplayMilliseconds + (DisplayMillisecondsPerCharacter * message.Length),
            MinDisplayMilliseconds,
            MaxDisplayMilliseconds));
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
