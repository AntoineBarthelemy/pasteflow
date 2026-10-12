namespace PasteFlow.Core.Matching;

/// <summary>
/// Whom a field is about: the user, who fills it with their own profile, or someone else.
/// </summary>
public enum FieldOwner
{
    /// <summary>
    /// Nothing tells: the profile only fills the field when the screen offers nothing else.
    /// </summary>
    Unknown,

    /// <summary>
    /// The user: "Votre e-mail", or any field of a page "Créer un compte".
    /// </summary>
    User,

    /// <summary>
    /// Someone else: "E-mail du contact", or any field of a page "Nouveau client".
    /// </summary>
    SomeoneElse,
}
