using System.Text.Json.Serialization;

namespace PasteFlow.Core.Profile;

/// <summary>
/// What an information of the profile of the user is about.
/// </summary>
/// <remarks>Saved by name, so that adding a topic never changes the meaning of a saved profile.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ProfileTopic>))]
public enum ProfileTopic
{
    /// <summary>
    /// The first name, such as "Antoine".
    /// </summary>
    FirstName,

    /// <summary>
    /// The last name, such as "Barthélémy".
    /// </summary>
    LastName,

    /// <summary>
    /// An email address.
    /// </summary>
    Email,

    /// <summary>
    /// A phone number.
    /// </summary>
    Phone,

    /// <summary>
    /// The street line of an address, such as "12 rue des Vignes".
    /// </summary>
    Street,

    /// <summary>
    /// A postal code.
    /// </summary>
    PostalCode,

    /// <summary>
    /// A city.
    /// </summary>
    City,

    /// <summary>
    /// A country.
    /// </summary>
    Country,

    /// <summary>
    /// The date of birth.
    /// </summary>
    BirthDate,

    /// <summary>
    /// A school or university.
    /// </summary>
    School,

    /// <summary>
    /// A company the user works for.
    /// </summary>
    Company,

    /// <summary>
    /// A job title, such as "Développeur web".
    /// </summary>
    JobTitle,

    /// <summary>
    /// The address of a LinkedIn profile.
    /// </summary>
    LinkedIn,

    /// <summary>
    /// The address of a website or portfolio.
    /// </summary>
    Website,

    /// <summary>
    /// An international bank account number. Sensitive.
    /// </summary>
    Iban,

    /// <summary>
    /// The code of a bank (BIC).
    /// </summary>
    Bic,

    /// <summary>
    /// A French social security number. Sensitive.
    /// </summary>
    SocialSecurityNumber,

    /// <summary>
    /// A passport number. Sensitive.
    /// </summary>
    PassportNumber,

    /// <summary>
    /// The number of an identity card. Sensitive.
    /// </summary>
    IdentityCardNumber,

    /// <summary>
    /// The number of a driving licence. Sensitive.
    /// </summary>
    DrivingLicenceNumber,

    /// <summary>
    /// Anything else, described by its own label.
    /// </summary>
    Other,
}
