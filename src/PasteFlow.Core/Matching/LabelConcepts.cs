using System.Text;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Reference;

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

    /// <summary>
    /// The concept of a whole name, first name and last name together: "Nom complet", "Full name".
    /// </summary>
    public const string FullName = "fullname";

    /// <summary>
    /// The word that makes a name a last name for sure: "Nom de famille".
    /// </summary>
    public const string FamilyName = "famille";

    /// <summary>
    /// A school or university.
    /// </summary>
    public const string School = "school";

    /// <summary>
    /// A job title.
    /// </summary>
    public const string JobTitle = "jobtitle";

    /// <summary>
    /// What is no longer true: "Ancienne adresse", "Loyer du précédent locataire".
    /// </summary>
    public const string Former = "former";

    /// <summary>
    /// The French social security number, « N° de sécurité sociale ».
    /// </summary>
    public const string SocialSecurity = "securitesociale";

    /// <summary>
    /// A registration number, such as the plate of a car.
    /// </summary>
    public const string Registration = "immatriculation";

    /// <summary>
    /// A measure with its unit: a surface, a weight, a distance.
    /// </summary>
    public const string Measure = "measure";

    /// <summary>
    /// A time of day: « Heure d'embauche », « Horaire ».
    /// </summary>
    public const string Time = "time";

    /// <summary>
    /// The start of a job: « Date d'embauche », « Prise de poste ».
    /// </summary>
    public const string Hiring = "embauche";

    /// <summary>
    /// What was really paid: « Net payé », « Montant versé ».
    /// </summary>
    public const string Paid = "paid";

    /// <summary>
    /// The sex or the title of a person: « Sexe », « Civilité ».
    /// </summary>
    public const string Sex = "sexe";

    private static readonly Dictionary<string, string> Synonyms = BuildSynonyms(new Dictionary<string, string[]>
    {
        [Email] = new[] { "email", "mail", "courriel", "emails", "electronique" },
        [Phone] = new[] { "telephone", "tel", "phone", "mobile", "portable", "gsm", "cellulaire", "fixe" },
        [Name] = new[] { "nom", "name", "surname", "lastname" },
        [FirstName] = new[] { "prenom", "firstname", "forename", "given" },
        [Address] = new[] { "rue", "street", "voie" },
        [City] = new[] { "ville", "city", "town", "commune", "localite", "lieu", "place", "endroit" },
        [PostalCode] = new[] { "postal", "cp", "zip", "postcode", "zipcode" },
        [Country] = new[] { "pays", "country" },
        [Company] = new[] { "entreprise", "societe", "company", "organisation", "organization", "employeur", "employer" },
        [Order] = new[] { "commande", "order", "reference", "ref", "booking", "reservation", "dossier" },
        [Invoice] = new[] { "facture", "invoice", "emission" },
        [Customer] = new[] { "client", "customer" },
        [Date] = new[] { "date", "jour", "day" },
        [Birth] = new[] { "naissance", "birth", "birthday", "dob", "ne", "nee" },
        [Amount] = new[] { "montant", "prix", "total", "amount", "price", "somme", "cout", "cost" },
        [BankAccount] = new[] { "iban", "rib" },
        [Website] = new[] { "site", "website", "url", "web", "lien", "link", "linkedin", "github", "portfolio" },
        [FullName] = new[] { "fullname", "complet" },
        [School] = new[] { "ecole", "etablissement", "universite", "university", "school", "iut", "college" },
        [JobTitle] = new[] { "poste", "fonction", "metier", "job", "profession", "jobtitle" },
        [Registration] = new[] { "immatriculation", "plaque" },
        [Measure] = new[] { "surface", "superficie", "poids", "distance", "kilometrage", "longueur", "largeur", "hauteur", "profondeur" },
        [Time] = new[] { "heure", "horaire", "time", "hour" },
        [Hiring] = new[] { "embauche", "hiring" },
        [Paid] = new[] { "paye", "payee", "verse", "versee", "paid" },
        [Sex] = new[] { "sexe", "genre", "civilite", "sex", "gender" },
        [Former] = new[] { "ancien", "ancienne", "anciens", "anciennes", "precedent", "precedente", "old", "former", "previous" },
    });

    // Alone, these words name the recipients of a message: "À", "Cc", "Destinataires"; in a longer label, such as
    // "Nom du destinataire", they do not mean an email
    private static readonly HashSet<string> RecipientWords = new(StringComparer.Ordinal)
    {
        "a", "to", "pour", "cc", "cci", "bcc", "destinataire", "destinataires", "recipient", "recipients",
    };

    private static readonly HashSet<string> NumberWords = new(StringComparer.Ordinal)
    {
        "n", "no", "num", "numero", "number", "nr", "nombre",
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "de", "du", "des", "la", "le", "les", "l", "d", "un", "une", "a", "au", "aux", "en", "et", "ou", "par", "pour", "sur",
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

        // The numbers of a company: 9 digits for the SIREN, 14 for the SIRET of one of its establishments
        ["siren"] = new[] { CandidateKind.Number },
        [SocialSecurity] = new[] { CandidateKind.Number },
        [Measure] = new[] { CandidateKind.Measure },
        [Time] = new[] { CandidateKind.Time },
        [Registration] = new[] { CandidateKind.Reference, CandidateKind.Number },
        ["siret"] = new[] { CandidateKind.Number },
    };

    private static readonly Dictionary<(string First, string Second), string> CompoundWords = new()
    {
        [("e", "mail")] = "email",
        [("first", "name")] = "firstname",
        [("full", "name")] = "fullname",
        [("given", "name")] = "firstname",
        [("last", "name")] = "lastname",
        [("family", "name")] = "lastname",
        [("sur", "name")] = "lastname",
        [("zip", "code")] = "zipcode",
        [("post", "code")] = "postcode",
        [("postal", "code")] = "postcode",
        [("securite", "sociale")] = SocialSecurity,
    };

    /// <summary>
    /// Words asking to type again the value of another field, such as "Confirmez votre email".
    /// </summary>
    private static readonly HashSet<string> ConfirmationVerbs = new(StringComparer.Ordinal)
    {
        "confirmer", "confirmez", "confirm", "repeter", "repetez", "repeat", "retype", "ressaisir", "ressaisissez", "again",
    };

    /// <summary>
    /// Words asking for a confirmation only next to a known piece of data: "Confirmation de l'email" repeats
    /// the email, while "Numéro de confirmation" is a value of its own.
    /// </summary>
    private static readonly HashSet<string> ConfirmationNouns = new(StringComparer.Ordinal) { "confirmation" };

    private static readonly HashSet<string> AddressWords = new(StringComparer.Ordinal) { "adresse", "address" };

    private static readonly HashSet<CandidateKind> ShapeKinds = new()
    {
        CandidateKind.Date, CandidateKind.Amount, CandidateKind.Email, CandidateKind.Phone, CandidateKind.Iban, CandidateKind.Url,
        CandidateKind.Time,
    };

    // The values of these concepts have no particular shape: they are recognized through the official lists or
    // through their labels, so plain text is accepted too. The first concept found wins: "Nom de l'entreprise" is a company
    private static readonly (string Concept, CandidateKind[] Kinds)[] TextConceptKinds =
    {
        (Company, new[] { CandidateKind.Company, CandidateKind.Text }),
        (City, new[] { CandidateKind.City, CandidateKind.Text }),
        (Country, new[] { CandidateKind.Country, CandidateKind.Text }),
        (Address, new[] { CandidateKind.Text }),
        (FullName, new[] { CandidateKind.FullName, CandidateKind.Text }),
    };

    // A place or a name of birth is a place or a name, not a date: "Ville de naissance", "Nom de naissance";
    // and "Nom du client" is a name, not a customer number
    private static readonly HashSet<string> PlaceOrPersonConcepts = new(StringComparer.Ordinal)
    {
        City, Country, Address, Name, FirstName, FullName,
    };

    private static readonly HashSet<string> QualifiedByPlaceOrPerson = new(StringComparer.Ordinal) { Birth, Customer, Order, Invoice };

    // Words that keep a name a person's name; any other word makes it unsure, as in "Nom d'utilisateur" or "Nom du projet"
    // The roles of a person in a document of work: « Nom du locataire » is still a person's name
    private static readonly HashSet<string> RoleWords = new(StringComparer.Ordinal)
    {
        "locataire", "locataires", "bailleur", "bailleurs", "debiteur", "debiteurs", "creancier", "creanciers", "patient",
        "patiente", "salarie", "salariee", "proprietaire", "titulaire", "assure", "assuree", "beneficiaire", "conjoint",
        "conjointe", "eleve", "acheteur", "acheteuse", "vendeur", "vendeuse", "mandataire", "gerant", "gerante",
        "signataire", "representant", "representante", "tuteur", "tutrice", "expediteur", "expeditrice",
    };

    private static readonly HashSet<string> PersonNameConcepts = new(StringComparer.Ordinal)
    {
        Name, FirstName, FullName, Birth, FamilyName, "usage",
    };

    /// <summary>
    /// Gets the concepts that only name the shape of a value, such as "date" or "email": next to a more specific word,
    /// as in "Date d'échéance", they do not tell which value is meant.
    /// </summary>
    public static IReadOnlySet<string> ShapeConcepts { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Email, Phone, Website, Date, Amount, Time,
    };

    /// <summary>
    /// Gets the concepts a label talks about. Words without a known synonym are kept as their own concept.
    /// </summary>
    /// <param name="label">The label, in any case, with or without accents.</param>
    /// <returns>
    /// The concepts, without the filler words such as "votre" or "please" and without the confirmation words,
    /// so that "Confirmez votre email" talks about the same thing as "Email".
    /// </returns>
    public static IReadOnlySet<string> From(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var words = Words(label);
        if (words.Count > 0 && words.TrueForAll(RecipientWords.Contains))
        {
            return new HashSet<string>(StringComparer.Ordinal) { Email };
        }

        var isConfirmation = IsConfirmation(words);
        var concepts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in words.Where(item => !AddressWords.Contains(item) && !(isConfirmation && IsConfirmationWord(item))))
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
    /// Determines whether a text asks to type again a value already asked, such as "Confirmez votre email"
    /// or "emailConfirmation" once cut into words.
    /// </summary>
    /// <param name="text">The label or the technical name of a field.</param>
    /// <returns><see langword="true"/> if the text asks for a confirmation; otherwise <see langword="false"/>.</returns>
    public static bool IsConfirmation(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return IsConfirmation(Words(text));
    }

    /// <summary>
    /// Gets the kinds of values a field described by these concepts expects.
    /// </summary>
    /// <param name="concepts">The concepts of the label of the field.</param>
    /// <returns>
    /// The expected kinds; for names, places and companies, the recognized kind and plain text;
    /// an empty set when the concepts give no clue.
    /// </returns>
    public static IReadOnlySet<CandidateKind> ExpectedKinds(IReadOnlySet<string> concepts)
    {
        return ExpectedKinds(concepts, firstNameAskedApart: false);
    }

    /// <summary>
    /// Gets the kinds of values a field described by these concepts expects, knowing the rest of the form.
    /// </summary>
    /// <param name="concepts">The concepts of the label of the field.</param>
    /// <param name="firstNameAskedApart">
    /// Whether the form has a field for the first name: then "Nom" means the last name only, otherwise the whole name.
    /// </param>
    /// <returns>
    /// The expected kinds; for names, places and companies, the recognized kind and plain text;
    /// an empty set when the concepts give no clue.
    /// </returns>
    public static IReadOnlySet<CandidateKind> ExpectedKinds(IReadOnlySet<string> concepts, bool firstNameAskedApart)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var isPlaceOrPerson = concepts.Any(PlaceOrPersonConcepts.Contains);
        var kinds = concepts
            .Where(concept => ExpectedKindsByConcept.ContainsKey(concept) && !(isPlaceOrPerson && QualifiedByPlaceOrPerson.Contains(concept)))
            .SelectMany(concept => ExpectedKindsByConcept[concept])
            .ToHashSet();

        // A word of shape names the value, the other words only say what it belongs to: "Date de facture" is a date,
        // "Montant de la commande" an amount, never the number of the invoice or of the order
        if (kinds.Overlaps(ShapeKinds))
        {
            kinds.IntersectWith(ShapeKinds);
        }

        if (kinds.Count > 0)
        {
            return kinds;
        }

        foreach (var (concept, textKinds) in TextConceptKinds)
        {
            if (concepts.Contains(concept))
            {
                return textKinds.ToHashSet();
            }
        }

        var hasName = concepts.Contains(Name);
        var hasFirstName = concepts.Contains(FirstName);
        if ((hasName || hasFirstName) && !concepts.All(concept => PersonNameConcepts.Contains(concept) || RoleWords.Contains(concept)))
        {
            // "Nom d'utilisateur", "Prénom de l'enfant": only the labels can tell
            return new HashSet<CandidateKind> { CandidateKind.Text };
        }

        if (hasName && hasFirstName)
        {
            // "Nom et prénom", "Prénom Nom"
            return new HashSet<CandidateKind> { CandidateKind.FullName, CandidateKind.Text };
        }

        if (hasFirstName)
        {
            return new HashSet<CandidateKind> { CandidateKind.FirstName, CandidateKind.Text };
        }

        if (hasName)
        {
            // « Nom du créancier » may be a person or a company, unless the form asks for the first name apart:
            // « Nom du salarié » next to « Prénom du salarié » is the last name only
            if (concepts.Overlaps(RoleWords) && !firstNameAskedApart)
            {
                return new HashSet<CandidateKind> { CandidateKind.FullName, CandidateKind.Company, CandidateKind.Text };
            }

            var isLastName = firstNameAskedApart || concepts.Contains(FamilyName);
            return new HashSet<CandidateKind> { isLastName ? CandidateKind.LastName : CandidateKind.FullName, CandidateKind.Text };
        }

        // A role alone, such as « Patient » or « Locataire », asks for the name of that person
        if (concepts.Count > 0 && concepts.All(RoleWords.Contains))
        {
            return new HashSet<CandidateKind> { CandidateKind.FullName, CandidateKind.Company, CandidateKind.Text };
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

        // "Langue" and "Langues" talk about the same thing: plain words are compared without their plural mark
        // nor "Salariée" and "Salarié": the feminine mark is folded too
        var secondFolded = second.Select(Fold).ToHashSet(StringComparer.Ordinal);
        var common = first.Count(concept => second.Contains(concept) || secondFolded.Contains(Fold(concept)));
        return 2.0 * common / (first.Count + second.Count);
    }

    /// <summary>
    /// Gets the concept of the first meaningful word of a label, the word that names what the field asks: « motif » in
    /// « Motif de la consultation ».
    /// </summary>
    /// <param name="label">The label of the field.</param>
    /// <returns>The concept, or <see langword="null"/> when the label has no meaningful word.</returns>
    public static string? HeadConcept(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        return Words(label).Select(word => From(word)).FirstOrDefault(concepts => concepts.Count > 0)?.First();
    }

    /// <summary>
    /// Tells whether a label asks for a number, such as "Numéro de permis" or "N° de dossier".
    /// </summary>
    /// <param name="label">The label of the field.</param>
    /// <returns><see langword="true"/> if the label asks for a number; otherwise <see langword="false"/>.</returns>
    public static bool AsksForNumber(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        return Words(label).Exists(NumberWords.Contains);
    }

    /// <summary>
    /// Removes the plural mark of a plain word of more than three letters, such as "langues" or "travaux".
    /// </summary>
    /// <param name="concept">A concept or a plain word.</param>
    /// <returns>The word without its final "s" or "x".</returns>
    private static string Fold(string concept)
    {
        var singular = concept.Length > 3 && (concept.EndsWith('s') || concept.EndsWith('x')) ? concept[..^1] : concept;
        return singular.Length > 4 && singular.EndsWith("ee", StringComparison.Ordinal) ? singular[..^1] : singular;
    }

    private static bool IsConfirmation(List<string> words)
    {
        return words.Exists(ConfirmationVerbs.Contains)
            || (words.Exists(ConfirmationNouns.Contains) && words.Exists(word => Synonyms.ContainsKey(word) || AddressWords.Contains(word)));
    }

    private static bool IsConfirmationWord(string word)
    {
        return ConfirmationVerbs.Contains(word) || ConfirmationNouns.Contains(word);
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
    /// Cuts a label into lower case words without accents, compound words such as "e-mail" or "first name"
    /// giving a single word.
    /// </summary>
    /// <param name="label">The label to cut.</param>
    /// <returns>The words of the label.</returns>
    private static List<string> Words(string label)
    {
        var normalized = ReferenceText.RemoveAccents(label).ToLowerInvariant();

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

        // « Prise de poste » and « prise de fonction » name the hiring, whatever the small word between
        for (var index = words.Count - 3; index >= 0; index--)
        {
            if (words[index] == "prise" && (words[index + 1] == "de" || words[index + 1] == "d")
                && words[index + 2] is "poste" or "fonction" or "fonctions")
            {
                words[index] = Hiring;
                words.RemoveRange(index + 1, 2);
            }
        }

        // Compound words such as "e mail" or "first name" are one word
        for (var index = words.Count - 2; index >= 0; index--)
        {
            if (CompoundWords.TryGetValue((words[index], words[index + 1]), out var compound))
            {
                words[index] = compound;
                words.RemoveAt(index + 1);
            }
        }

        return words;
    }
}
