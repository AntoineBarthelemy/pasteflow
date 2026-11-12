using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Logins;

/// <summary>
/// The password manager of the user, such as Bitwarden: PasteFlow never keeps a password, it asks for it here at the
/// moment it writes it.
/// </summary>
/// <remarks>Implementations never throw, except on cancellation: an unavailable manager finds nothing.</remarks>
public interface IPasswordManager
{
    /// <summary>
    /// Gets a value indicating whether the manager is unlocked and can answer.
    /// </summary>
    bool IsUnlocked { get; }

    /// <summary>
    /// Gets a value indicating whether the manager is set up on this computer: a sign-in page is then left to it, even
    /// locked, rather than filled from the profile.
    /// </summary>
    bool IsInstalled => true;

    /// <summary>
    /// Finds the accounts saved for a site.
    /// </summary>
    /// <param name="host">The host of the page, such as "github.com".</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The accounts the manager gives for this address; the caller checks the site again.</returns>
    Task<IReadOnlyList<VaultLogin>> FindLoginsForSiteAsync(string host, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the accounts whose name looks like a word, such as the name of an application.
    /// </summary>
    /// <param name="text">The word searched, such as "steam".</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The accounts found.</returns>
    Task<IReadOnlyList<VaultLogin>> SearchLoginsAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the password of an account.
    /// </summary>
    /// <param name="id">The identifier of the account.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The password, or <see langword="null"/> if it cannot be read.</returns>
    Task<string?> GetPasswordAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the identity the user keeps in the manager: name, address, phone, and so on.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The values of the identity, or an empty list when there is none, or several.</returns>
    Task<IReadOnlyList<ProfileValue>> GetIdentityAsync(CancellationToken cancellationToken);
}
