using PasteFlow.Core.Forms;

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

    /// <summary>
    /// Shows the result of a fill. By default, as a text message; a richer recap can be shown instead.
    /// </summary>
    /// <param name="report">The result of the fill.</param>
    void NotifyFill(FillReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        Notify(report.Describe());
    }
}
