using PasteFlow.Core.Forms;
using PasteFlow.Core.Notifications;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Displays messages and recaps of fills in <see cref="NotificationPopup"/> windows, up to three stacked in the bottom
/// right corner.
/// </summary>
/// <remarks>
/// Must be used from the user interface thread.
/// </remarks>
internal sealed class PopupNotifier : IUserNotifier
{
    // A few popups stay on screen at once, newest at the bottom: a new message never hides the one before it
    private const int MaxPopups = 3;
    private const double StackGap = 8;

    private readonly List<NotificationPopup> _popups = new();

    /// <inheritdoc/>
    public void Notify(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Show(new NotificationPopup(message));
    }

    /// <inheritdoc/>
    public void NotifyFill(FillReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var recap = FillRecap.From(report);
        Show(new NotificationPopup(RecapView.Create(recap), RecapView.DisplayTimeFor(recap)));
    }

    /// <summary>
    /// Displays any content, such as a message with a button, above the popups already displayed.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="displayTime">How long the popup stays when the mouse is not on it.</param>
    public void ShowContent(System.Windows.UIElement content, TimeSpan displayTime)
    {
        ArgumentNullException.ThrowIfNull(content);

        Show(new NotificationPopup(content, displayTime));
    }

    private void Show(NotificationPopup popup)
    {
        // Past the limit, the oldest popup gives its place
        if (_popups.Count == MaxPopups)
        {
            _popups[0].Close();
        }

        popup.Closed += (_, _) =>
        {
            _ = _popups.Remove(popup);
            Restack();
        };
        popup.Loaded += (_, _) => Restack();
        _popups.Add(popup);
        popup.Show();
    }

    private void Restack()
    {
        // From the newest, at the bottom, upwards
        var offset = 0.0;
        for (var index = _popups.Count - 1; index >= 0; index--)
        {
            var popup = _popups[index];
            popup.StackOffset = offset;
            popup.Place();
            offset += popup.CardHeight + StackGap;
        }
    }
}
