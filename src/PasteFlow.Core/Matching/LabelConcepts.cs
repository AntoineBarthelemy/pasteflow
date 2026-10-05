using System.Globalization;
using System.Text;
using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Matching;

/// <summary>
/// Turns a label into the concepts it talks about, so that labels written differently can be compared:
/// "E-mail", "Courriel" and "Adresse mail" all talk about an email; "Référence" and "Commande n°" about an order.
/// </summary>
public static class LabelConcepts
{
    /// <summary>
    /// Concept of an email address.
    /// </summary>
    public const string Email = "email";

    /// <summary>
    /// Concept of a phone number.
    /// </summary>
    public const string Phone = "phone";

    /// <summary>
    /// Concept of a name.
    /// </summary>
    public const string Name = "name";

    /// <summary>
    /// Concept of a first name.
    /// </summary>
    public const string FirstName = "firstname";

    /// <summary>
    /// Concept of a postal address.
    /// </summary>
    public const string Address = "address";

    /// <summary>
    /// Concept of a city.
    /// </summary>
    public const string City = "city";

    /// <summary>
    /// Concept of a postal code.
    /// </summary>
    public const string PostalCode = "postalcode";

    /// <summary>
    /// Concept of a country.
    /// </summary>
    public const string Country = "country";

    /// <summary>
    /// Concept of a company.
    /// </summary>
    public const string Company = "company";

    /// <summary>
    /// Concept of an order or booking reference.
    /// </summary>
    public const string Order = "order";

    /// <summary>
    /// Concept of an invoice.
    /// </summary>
    public const string Invoice = "invoice";

    /// <summary>
    /// Concept of a customer.
    /// </summary>
    public const string Customer = "customer";

    /// <summary>
    /// Concept of a date.
    /// </summary>
    public const string Date = "date";

    /// <summary>
    /// Concept of a birth.
    /// </summary>
    public const string Birth = "birth";

    /// <summary>
    /// Concept of an amount of money.
    /// </summary>
    public const string Amount = "amount";

    /// <summary>
    /// Concept of a bank account.
    /// </summary>
    public const string BankAccount = "iban";

    /// <summary>
    /// Concept of a web address.
    /// </summary>
    public const string Website = "website";

    private static readonly Dictionary<string, string> Synonyms = BuildSynonyms(new Dictionary<string, string[]>
    {
        [Email] = new[] { "email", "mail", "courriel", "emails", "electronique" },
        [Phone] = new[] { "telephone", "tel", "phone", "mobile", "portable", "gsm", "cellulaire", "fixe" },
        [Name] = new[] { "nom", "name", "surname", "lastname" },
        [FirstName] = new[] { "prenom", "firstname", "forename", "given" },
        [Address] = new[] { "rue", "street", "voie" },
        [City] = new[] { "ville", "city", "town", "commune", "localite" },
        [PostalCode] = new[] { "postal", "cp", "zip", "postcode", "zipcode" },
        [Country] = new[] { "pays", "country" },
        [Company] = new[] { "entreprise", "societe", "company", "organisation", "organization", "employeur", "employer" },
        [Order] = new[] { "commande", "order", "reference", "ref", "booking", "reservation", "dossier" },
        [Invoice] = new[] { "facture", "invoice" },
        [Customer] = new[] { "client", "customer" },
        [Date] = new[] { "date", "jour", "day" },
        [Birth] = new[] { "naissance", "birth", "birthday", "dob", "ne", "nee" },
        [Amount] = new[] { "montant", "prix", "total", "amount", "price", "somme", "cout", "cost" },
        [BankAccount] = new[] { "iban", "rib" },
        [Website] = new[] { "site", "website", "url", "web", "lien", "link", "linkedin", "github", "portfolio" },
    });

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "de", "du", "des", "la", "le", "les", "l", "d", "un", "une", "a", "au", "aux", "en", "et", "ou", "par", "pour",
        "votre", "vos", "ton", "tes", "mon", "mes", "son", "ses", "n", "no", "num", "numero", "number", "nr",
        "the", "of", "your", "my", "an", "and", "or", "for", "please", "veuillez", "saisir", "saisissez",
        "entrez", "enter", "indiquez", "obligatoire", "required", "optionnel", "optional",
    };

    private static readonly Dictionary<string, CandidateKind[]> ExpectedKindsByConcept = new(StringComparer.Ordinal)
    {
        [Email] = new[] { CandidateKind.Email },
        [Phone] = new[] { CandidateKind.Phone },
        [Website] = new[] { CandidateKind.Url },
        [Date] = new[] { CandidateKind.Date },
        [Birth] = new[] { CandidateKind.Date },
        [Amount] = new[] { CandidateKind.Amount },
        [BankAccount] = new[] { CandidateKind.Iban },
        [Order] = new[] { CandidateKind.Reference, CandidateKind.Number },
        [Invoice] = new[] { CandidateKind.Reference, CandidateKind.Number },
        [Customer] = new[] { CandidateKind.Reference, CandidateKind.Number },
        [PostalCode] = new[] { CandidateKind.Number },
    };

    private static readonly HashSet<string> AddressWords = new(StringComparer.Ordinal) { "adresse", "address" };

    private static readonly HashSet<string> TextConcepts = new(StringComparer.Ordinal)
    {
        Name, FirstName, Address, City, Country, Company,
    };

    /// <summary>
    /// Gets the concepts a label talks about. Words without a known synonym are kept as their own concept.
    /// </summary>
    /// <param name="label">The label, in any case, with or without accents.</param>
    /// <returns>The concepts, without the filler words such as "votre" or "please".</returns>
    public static IReadOnlySet<string> From(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var words = Words(label);
        var concepts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in words.Where(item => !AddressWords.Contains(item)))
        {
            var concept = Synonyms.TryGetValue(word, out var known) ? known : word;
            if (!StopWords.Contains(concept))
            {
                _ = concepts.Add(concept);
            }
        }

        // "Adresse mail" talks about an email, "adresse postale" or "adresse" alone about a postal address
        if (words.Exists(AddressWords.Contains) && !concepts.Contains(Email) && !concepts.Contains(Website))
        {
            _ = concepts.Add(Address);
            if (!words.Contains("code"))
            {
                _ = concepts.Remove(PostalCode);
            }
        }

        return concepts;
    }

    /// <summary>
    /// Gets the kinds of values a field described by these concepts expects.
    /// </summary>
    /// <param name="concepts">The concepts of the label of the field.</param>
    /// <returns>
    /// The expected kinds; <see cref="CandidateKind.Text"/> alone for names, addresses and the like;
    /// an empty set when the concepts give no clue.
    /// </returns>
    public static IReadOnlySet<CandidateKind> ExpectedKinds(IReadOnlySet<string> concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var kinds = concepts
            .Where(ExpectedKindsByConcept.ContainsKey)
            .SelectMany(concept => ExpectedKindsByConcept[concept])
            .ToHashSet();

        if (kinds.Count == 0 && concepts.Any(TextConcepts.Contains))
        {
            _ = kinds.Add(CandidateKind.Text);
        }

        return kinds;
    }

    /// <summary>
    /// Measures how much two sets of concepts overlap.
    /// </summary>
    /// <param name="first">The first set of concepts.</param>
    /// <param name="second">The second set of concepts.</param>
    /// <returns>A value from 0 (nothing in common) to 1 (same concepts), the Dice coefficient.</returns>
    public static double Similarity(IReadOnlySet<string> first, IReadOnlySet<string> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count == 0 || second.Count == 0)
        {
            return 0;
        }

        var common = first.Count(second.Contains);
        return 2.0 * common / (first.Count + second.Count);
    }

    private static Dictionary<string, string> BuildSynonyms(Dictionary<string, string[]> groups)
    {
        var synonyms = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (concept, words) in groups)
        {
            foreach (var word in words)
            {
                synonyms[word] = concept;
            }
        }

        return synonyms;
    }

    /// <summary>
    /// Cuts a label into lower case words without accents, "e-mail" being one word.
    /// </summary>
    /// <param name="label">The label to cut.</param>
    /// <returns>The words of the label.</returns>
    private static List<string> Words(string label)
    {
        var normalized = RemoveAccents(label).ToLowerInvariant()
            .Replace("e-mail", "email", StringComparison.Ordinal)
            .Replace("e mail", "email", StringComparison.Ordinal);

        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                _ = word.Append(character);
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                _ = word.Clear();
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    private static string RemoveAccents(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                _ = builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
