using PasteFlow.Core.Notifications;

namespace PasteFlow.App.Notifications;

/// <summary>
/// Displays messages in a <see cref="NotificationPopup"/>, one at a time.
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

        // A new message replaces the one currently displayed
        _currentPopup?.Close();

        var popup = new NotificationPopup(message);
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
