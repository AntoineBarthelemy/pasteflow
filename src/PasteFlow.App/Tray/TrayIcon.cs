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
    /// <param name="clearHistoryRequested">The action to run when the user chooses to clear the history of fills.</param>
    /// <param name="settingsRequested">The action to run when the user chooses to open the settings.</param>
    /// <param name="profileRequested">The action to run when the user chooses to open their profile.</param>
    /// <param name="bitwardenRequested">The action to run when the user chooses to unlock or lock Bitwarden.</param>
    /// <param name="isBitwardenUnlocked">Tells, each time the menu opens, whether Bitwarden is unlocked.</param>
    public TrayIcon(
        Action exitRequested,
        Action clearHistoryRequested,
        Action settingsRequested,
        Action profileRequested,
        Action bitwardenRequested,
        Func<bool> isBitwardenUnlocked)
    {
        ArgumentNullException.ThrowIfNull(exitRequested);
        ArgumentNullException.ThrowIfNull(clearHistoryRequested);
        ArgumentNullException.ThrowIfNull(settingsRequested);
        ArgumentNullException.ThrowIfNull(profileRequested);
        ArgumentNullException.ThrowIfNull(bitwardenRequested);
        ArgumentNullException.ThrowIfNull(isBitwardenUnlocked);

        _menu = new ContextMenuStrip();
        _ = _menu.Items.Add("Ma fiche…", image: null, (_, _) => profileRequested());
        var bitwarden = _menu.Items.Add(BitwardenMenuText(false), image: null, (_, _) => bitwardenRequested());
        _ = _menu.Items.Add("Paramètres…", image: null, (_, _) => settingsRequested());
        _ = _menu.Items.Add("Effacer l'historique", image: null, (_, _) => clearHistoryRequested());
        _ = _menu.Items.Add(new ToolStripSeparator());
        _ = _menu.Items.Add("Quitter", image: null, (_, _) => exitRequested());

        // The menu tells the real state of Bitwarden each time it opens
        _menu.Opening += (_, _) => bitwarden.Text = BitwardenMenuText(isBitwardenUnlocked());

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

    /// <summary>
    /// Gets the entry of the menu for Bitwarden, which tells its state.
    /// </summary>
    /// <param name="isUnlocked">Whether Bitwarden is unlocked.</param>
    /// <returns>The text of the entry.</returns>
    internal static string BitwardenMenuText(bool isUnlocked)
    {
        return isUnlocked ? "Bitwarden déverrouillé ✓ (cliquer pour verrouiller)" : "Déverrouiller Bitwarden…";
    }
}
