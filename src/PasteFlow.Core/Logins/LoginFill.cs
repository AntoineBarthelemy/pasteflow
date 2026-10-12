using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Logins;

/// <summary>
/// What was written into a sign-in form from the password manager.
/// </summary>
public sealed record LoginFill
{
    /// <summary>
    /// Gets the name of the account used, such as "GitHub".
    /// </summary>
    public required string AccountName { get; init; }

    /// <summary>
    /// Gets the field of the identifier written, or <see langword="null"/> if none was written.
    /// </summary>
    public FormField? IdentifierField { get; init; }

    /// <summary>
    /// Gets the identifier written, or an empty string.
    /// </summary>
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    /// Gets the password field.
    /// </summary>
    public required FormField PasswordField { get; init; }

    /// <summary>
    /// Gets a value indicating whether the password was written.
    /// </summary>
    public bool IsPasswordWritten { get; init; }
}
