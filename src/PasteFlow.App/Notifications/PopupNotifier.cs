using PasteFlow.Core.Forms;
using PasteFlow.Core.Notifications;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Displays messages and recaps of fills in a <see cref="NotificationPopup"/>, one at a time.
/// </summary>
/// <remarks>
/// Must be used from the user interface thread.
/// </remarks>
internal sealed class PopupNotifier : IUserNotifier
{
    private NotificationPopup? _currentPopup;

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

    private void Show(NotificationPopup popup)
    {
        // A new popup replaces the one currently displayed
        _currentPopup?.Close();

        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_currentPopup, popup))
            {
                _currentPopup = null;
            }
        };

        _currentPopup = popup;
        popup.Show();
    }
}
