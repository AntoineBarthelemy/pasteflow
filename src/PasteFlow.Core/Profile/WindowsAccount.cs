namespace PasteFlow.Core.Profile;

/// <summary>
/// What Windows knows about its user: the name and the email of their account. Each part may be empty.
/// </summary>
public sealed record WindowsAccount
{
    /// <summary>
    /// Gets the first name of the account, or an empty string.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the last name of the account, or an empty string.
    /// </summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the email of the Microsoft account, or an empty string for a local account.
    /// </summary>
    public string Email { get; init; } = string.Empty;
}
