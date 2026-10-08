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

    /// <summary>
    /// A known first name, such as "Antoine".
    /// </summary>
    FirstName,

    /// <summary>
    /// A last name next to a known first name, such as "Barthélémy" in "Antoine Barthélémy".
    /// </summary>
    LastName,

    /// <summary>
    /// A first name followed or preceded by a last name, such as "Antoine Barthélémy".
    /// </summary>
    FullName,

    /// <summary>
    /// A French commune, recognized next to its postal code or alone on an address line.
    /// </summary>
    City,

    /// <summary>
    /// A country, alone on its line.
    /// </summary>
    Country,

    /// <summary>
    /// A company name, recognized through its legal form, such as "Reims Auto SARL".
    /// </summary>
    Company,
}
