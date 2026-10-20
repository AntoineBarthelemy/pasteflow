using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace PasteFlow.App.Tray;

/// <summary>
/// Icon of the application in the notification area of the taskbar; a click, left or right, opens the menu of
/// PasteFlow, the same one as its shortcut.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const string ProductName = "PasteFlow";

    // Windows truncates notification area tooltips beyond this length
    private const int MaxTooltipLength = 127;

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon? _icon;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIcon"/> class and makes it visible.
    /// </summary>
    /// <param name="menuRequested">The action to run when the user clicks the icon.</param>
    public TrayIcon(Action menuRequested)
    {
        ArgumentNullException.ThrowIfNull(menuRequested);

        _icon = LoadIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon ?? SystemIcons.Application,
            Text = ProductName,
            Visible = true,
        };
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button is MouseButtons.Left or MouseButtons.Right)
            {
                menuRequested();
            }
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
        _icon?.Dispose();
    }

    private static Icon? LoadIcon()
    {
        // The size of the notification area, sharp on screens zoomed in
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PasteFlow.App.Assets.PasteFlow.ico");
        return stream is null ? null : new Icon(stream, SystemInformation.SmallIconSize);
    }
}
