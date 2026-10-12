namespace PasteFlow.Core.Logins;

/// <summary>
/// Lets the user choose the account to sign in with, when PasteFlow cannot be sure of it.
/// </summary>
public interface ILoginPicker
{
    /// <summary>
    /// Shows the accounts and waits for the choice of the user.
    /// </summary>
    /// <param name="logins">The accounts to choose from.</param>
    /// <param name="cancellationToken">A token to cancel the choice.</param>
    /// <returns>The account chosen, or <see langword="null"/> if the user cancelled.</returns>
    Task<VaultLogin?> ChooseAsync(IReadOnlyList<VaultLogin> logins, CancellationToken cancellationToken);
}
