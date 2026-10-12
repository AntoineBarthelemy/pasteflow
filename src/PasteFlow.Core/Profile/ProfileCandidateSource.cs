using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Profile;

/// <summary>
/// Offers the values of the profile of the user to the fills, each with the kind and the label that let the matcher
/// recognize the fields it fits, such as "E-mail" for an email.
/// </summary>
/// <remarks>
/// A first name and a last name also make a whole name, for the forms that ask "Nom complet". Whether a value fills
/// a field is decided with the screen by <c>ProfileArbiter</c>.
/// </remarks>
public sealed class ProfileCandidateSource : ICandidateSource
{
    /// <summary>
    /// The name shown as the source of the values offered.
    /// </summary>
    public const string SourceName = "Ma fiche";

    private readonly ProfileBook _book;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileCandidateSource"/> class.
    /// </summary>
    /// <param name="book">The profile of the user.</param>
    public ProfileCandidateSource(ProfileBook book)
    {
        ArgumentNullException.ThrowIfNull(book);

        _book = book;
    }

    /// <summary>
    /// Gets the kind and the label under which a value of a topic is offered.
    /// </summary>
    /// <param name="value">The value of the profile.</param>
    /// <returns>The kind of value and a label the matcher understands.</returns>
    public static (CandidateKind Kind, string Label) DescriptionOf(ProfileValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Topic switch
        {
            ProfileTopic.FirstName => (CandidateKind.FirstName, "Prénom"),
            ProfileTopic.LastName => (CandidateKind.LastName, "Nom"),
            ProfileTopic.Email => (CandidateKind.Email, "E-mail"),
            ProfileTopic.Phone => (CandidateKind.Phone, "Téléphone"),
            ProfileTopic.Street => (CandidateKind.Text, "Adresse"),
            ProfileTopic.PostalCode => (CandidateKind.Number, "Code postal"),
            ProfileTopic.City => (CandidateKind.City, "Ville"),
            ProfileTopic.Country => (CandidateKind.Country, "Pays"),
            ProfileTopic.BirthDate => (CandidateKind.Date, "Date de naissance"),
            ProfileTopic.School => (CandidateKind.Text, "École"),
            ProfileTopic.Company => (CandidateKind.Company, "Entreprise"),
            ProfileTopic.JobTitle => (CandidateKind.Text, "Poste"),
            ProfileTopic.LinkedIn => (CandidateKind.Url, "LinkedIn"),
            ProfileTopic.Website => (CandidateKind.Url, "Site web"),
            ProfileTopic.Iban => (CandidateKind.Iban, "IBAN"),
            ProfileTopic.Bic => (CandidateKind.Text, "BIC"),
            ProfileTopic.SocialSecurityNumber => (CandidateKind.Text, "Numéro de sécurité sociale"),
            ProfileTopic.PassportNumber => (CandidateKind.Text, "Numéro de passeport"),
            ProfileTopic.IdentityCardNumber => (CandidateKind.Text, "Numéro de carte d'identité"),
            ProfileTopic.DrivingLicenceNumber => (CandidateKind.Text, "Numéro de permis de conduire"),
            _ => (CandidateKind.Text, value.Label.Length > 0 ? value.Label : "Autre"),
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Candidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var values = await _book.GetValuesAsync(cancellationToken);
        var candidates = values.Select(ToCandidate).ToList();

        var firstNames = values.Where(value => value.Topic == ProfileTopic.FirstName).ToList();
        var lastNames = values.Where(value => value.Topic == ProfileTopic.LastName).ToList();
        if (firstNames.Count == 1 && lastNames.Count == 1)
        {
            candidates.Add(new Candidate
            {
                Value = $"{firstNames[0].Value} {lastNames[0].Value}",
                Kind = CandidateKind.FullName,
                Label = "Nom complet",
                SourceName = SourceName,
                SourceRank = 0,
                IsFromProfile = true,
                ProfileUseCount = Math.Min(firstNames[0].UseCount, lastNames[0].UseCount),
            });
        }

        return candidates;
    }

    private static Candidate ToCandidate(ProfileValue value)
    {
        var (kind, label) = DescriptionOf(value);
        return new Candidate
        {
            Value = value.Value,
            Kind = kind,
            Label = label,
            SourceName = SourceName,
            SourceRank = 0,
            IsFromProfile = true,
            IsSensitive = value.IsSensitive,
            ProfileSites = value.Sites,
            ProfileUseCount = value.UseCount,
        };
    }
}
