using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PasteFlow.App.Design;

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
    private const double ScreenMargin = 20;
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

        // A white card with a soft shadow, in the bottom right corner
        Content = Ui.PopupRoot(content, new Thickness(16, 14, 16, 14));

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

    private static Grid CreateMessageContent(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The mark of PasteFlow, then the message
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var mark = Ui.Icon(Icons.Paste, 14, Theme.Brush(palette => palette.Accent));
        mark.VerticalAlignment = VerticalAlignment.Top;
        mark.Margin = new Thickness(0, 2, 12, 0);
        grid.Children.Add(mark);
        var text = Ui.Text(message, TextRole.Body);
        text.MaxWidth = MaxTextWidth;
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
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

        // The room left for the shadow already keeps the card away from the edges
        Left = workArea.Right - ActualWidth - (ScreenMargin - Ui.ShadowMargin);
        Top = workArea.Bottom - ActualHeight - (ScreenMargin - Ui.ShadowMargin);
        _closeTimer.Start();
    }

    private void OnCloseTimerTick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        Close();
    }
}
