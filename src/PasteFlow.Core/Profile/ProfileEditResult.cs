namespace PasteFlow.Core.Profile;

/// <summary>
/// What became of a change asked to the profile.
/// </summary>
public enum ProfileEditResult
{
    /// <summary>
    /// The change is made and saved.
    /// </summary>
    Done,

    /// <summary>
    /// The change is made, but could not be saved: it is lost when PasteFlow closes.
    /// </summary>
    NotSaved,

    /// <summary>
    /// The profile already has this value for this topic: nothing changed.
    /// </summary>
    AlreadyThere,

    /// <summary>
    /// The value is empty.
    /// </summary>
    Empty,

    /// <summary>
    /// The value is too long to be an information of the profile.
    /// </summary>
    TooLong,

    /// <summary>
    /// The value does not have the format of its topic: an email without "@", an IBAN with a wrong key...
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// The value is a payment card number, which PasteFlow never keeps.
    /// </summary>
    PaymentCardRefused,

    /// <summary>
    /// The value to change or remove is no longer in the profile.
    /// </summary>
    NotFound,

    /// <summary>
    /// The profile is full.
    /// </summary>
    Full,
}
