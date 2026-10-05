using System.Drawing;
using System.Windows.Forms;
using PasteFlow.Core.Notifications;

namespace PasteFlow.App.Tray;

/// <summary>
/// Icon of the application in the notification area of the taskbar, also used to display notifications.
/// </summary>
internal sealed class TrayIcon : IUserNotifier, IDisposable
{
    private const string ProductName = "PasteFlow";
    private const int NotificationTimeoutMilliseconds = 2000;

    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon"/> class and makes it visible.
    /// </summary>
    /// <param name="exitRequested">The action to run when the user chooses to quit the application.</param>
    public TrayIcon(Action exitRequested)
    {
        ArgumentNullException.ThrowIfNull(exitRequested);

        _menu = new ContextMenuStrip();
        _ = _menu.Items.Add("Quitter", image: null, (_, _) => exitRequested());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = ProductName,
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    /// <inheritdoc/>
    public void Notify(string message)
    {
        _notifyIcon.ShowBalloonTip(NotificationTimeoutMilliseconds, ProductName, message, ToolTipIcon.Info);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}
