namespace PasteFlow.Core.Notifications;

/// <summary>
/// Displays short and non-blocking messages to the user.
/// </summary>
public interface IUserNotifier
{
    /// <summary>
    /// Shows a short message to the user.
    /// </summary>
    /// <param name="message">The message to show, in the language of the user interface.</param>
    void Notify(string message);
}
