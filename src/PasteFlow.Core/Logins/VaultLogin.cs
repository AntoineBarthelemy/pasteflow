namespace PasteFlow.Core.Logins;

/// <summary>
/// An account kept by the password manager of the user, without its password: the password is only asked for at the
/// moment it is written.
/// </summary>
public sealed record VaultLogin
{
    /// <summary>
    /// Gets the identifier of the account in the password manager.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the name of the account, such as "GitHub".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the identifier the user signs in with, such as their email, or an empty string.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the addresses of the sites where the account is used.
    /// </summary>
    public IReadOnlyList<string> Uris { get; init; } = Array.Empty<string>();
}
