namespace PasteFlow.Core.Profile;

/// <summary>
/// What PasteFlow knows about each topic of the profile: its name shown to the user and whether it is sensitive.
/// </summary>
public static class ProfileTopics
{
    private static readonly HashSet<ProfileTopic> SensitiveTopics = new()
    {
        ProfileTopic.Iban, ProfileTopic.SocialSecurityNumber, ProfileTopic.PassportNumber,
        ProfileTopic.IdentityCardNumber, ProfileTopic.DrivingLicenceNumber,
    };

    /// <summary>
    /// Gets every topic, in the order shown to the user.
    /// </summary>
    public static IReadOnlyList<ProfileTopic> All { get; } = Enum.GetValues<ProfileTopic>();

    /// <summary>
    /// Determines whether the values of a topic are sensitive: protected by Windows Hello, never learned on their own.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <returns><see langword="true"/> if the topic is sensitive; otherwise <see langword="false"/>.</returns>
    public static bool IsSensitive(ProfileTopic topic)
    {
        return SensitiveTopics.Contains(topic);
    }

    /// <summary>
    /// Gets the name of a topic shown to the user.
    /// </summary>
    /// <param name="topic">The topic.</param>
    /// <returns>The name, in French.</returns>
    public static string DisplayName(ProfileTopic topic)
    {
        return topic switch
        {
            ProfileTopic.FirstName => "Prénom",
            ProfileTopic.LastName => "Nom",
            ProfileTopic.Email => "E-mail",
            ProfileTopic.Phone => "Téléphone",
            ProfileTopic.Street => "Adresse",
            ProfileTopic.PostalCode => "Code postal",
            ProfileTopic.City => "Ville",
            ProfileTopic.Country => "Pays",
            ProfileTopic.BirthDate => "Date de naissance",
            ProfileTopic.School => "École",
            ProfileTopic.Company => "Entreprise",
            ProfileTopic.JobTitle => "Poste",
            ProfileTopic.LinkedIn => "LinkedIn",
            ProfileTopic.Website => "Site web",
            ProfileTopic.Iban => "IBAN",
            ProfileTopic.Bic => "BIC",
            ProfileTopic.SocialSecurityNumber => "N° de sécurité sociale",
            ProfileTopic.PassportNumber => "N° de passeport",
            ProfileTopic.IdentityCardNumber => "N° de carte d'identité",
            ProfileTopic.DrivingLicenceNumber => "N° de permis de conduire",
            _ => "Autre",
        };
    }
}
