namespace PasteFlow.Core.Candidates;

/// <summary>
/// Kind of value recognized in a piece of text.
/// </summary>
public enum CandidateKind
{
    /// <summary>
    /// Free text, no particular format recognized.
    /// </summary>
    Text,

    /// <summary>
    /// An email address.
    /// </summary>
    Email,

    /// <summary>
    /// A phone number.
    /// </summary>
    Phone,

    /// <summary>
    /// A web address.
    /// </summary>
    Url,

    /// <summary>
    /// A date written with digits, such as 12/10/2026 or 2026-10-12.
    /// </summary>
    Date,

    /// <summary>
    /// An amount of money with its currency.
    /// </summary>
    Amount,

    /// <summary>
    /// An international bank account number.
    /// </summary>
    Iban,

    /// <summary>
    /// A code mixing upper case letters and digits, such as an order number.
    /// </summary>
    Reference,

    /// <summary>
    /// A long number, such as a customer number or a postal code.
    /// </summary>
    Number,
}
