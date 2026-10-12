using PasteFlow.Core.Logins;
using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// A vault kept in memory: it answers like Bitwarden would, and counts the passwords asked for.
/// </summary>
internal sealed class FakePasswordManager : IPasswordManager
{
    public bool IsUnlocked { get; set; } = true;

    public List<VaultLogin> Logins { get; } = new();

    public Dictionary<string, string> Passwords { get; } = new(StringComparer.Ordinal);

    public List<ProfileValue> Identity { get; } = new();

    public int PasswordReads { get; private set; }

    public Task<IReadOnlyList<VaultLogin>> FindLoginsForSiteAsync(string host, CancellationToken cancellationToken)
    {
        // Like Bitwarden with a loose match: the caller must check the site again
        return Task.FromResult<IReadOnlyList<VaultLogin>>(Logins.Where(login => login.Uris.Any(uri => uri.Contains(host.Split('.')[0], StringComparison.OrdinalIgnoreCase))).ToList());
    }

    public Task<IReadOnlyList<VaultLogin>> SearchLoginsAsync(string text, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<VaultLogin>>(Logins.Where(login => login.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    public Task<string?> GetPasswordAsync(string id, CancellationToken cancellationToken)
    {
        PasswordReads++;
        return Task.FromResult(Passwords.TryGetValue(id, out var password) ? password : null);
    }

    public Task<IReadOnlyList<ProfileValue>> GetIdentityAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ProfileValue>>(Identity);
    }
}
