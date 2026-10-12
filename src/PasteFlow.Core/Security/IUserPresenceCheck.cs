namespace PasteFlow.Core.Security;

/// <summary>
/// Checks that the user in front of the computer is really its owner, before a sensitive value is used.
/// </summary>
public interface IUserPresenceCheck
{
    /// <summary>
    /// Asks the user to prove it is them.
    /// </summary>
    /// <param name="reason">Why, shown to the user, such as "Afficher ton IBAN".</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>What came of the check. Never throws, except on cancellation.</returns>
    Task<PresenceResult> VerifyAsync(string reason, CancellationToken cancellationToken);
}
