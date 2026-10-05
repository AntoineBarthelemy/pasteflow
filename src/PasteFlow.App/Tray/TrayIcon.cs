using System.Drawing;
using System.Windows.Forms;

namespace PasteFlow.App.Tray;

/// <summary>
/// Icon of the application in the notification area of the taskbar.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const string ProductName = "PasteFlow";

    // Windows truncates notification area tooltips beyond this length
    private const int MaxTooltipLength = 127;

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

    /// <summary>
    /// Shows the state of the application in the tooltip of the icon.
    /// </summary>
    /// <param name="status">A short status, for example "Ctrl+Alt+V actif".</param>
    public void ShowStatus(string status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var text = $"{ProductName} : {status}";
        _notifyIcon.Text = text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}
