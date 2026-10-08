using System.Buffers;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Candidates;

/// <summary>
/// Recognizes values that have no particular shape thanks to the official lists: people, communes, countries,
/// and companies through their legal form.
/// </summary>
/// <remarks>
/// <para>The rules favor precision: many first names are also common words ("Rose", "France") and many communes too
/// ("Rue", "Orange"), so a value is recognized only when its context leaves little doubt.</para>
/// <list type="bullet">
/// <item><description>a person is 2 to 4 capitalized words, one end being a known first name, such as
/// "Antoine Barthélémy" or "BARTHÉLÉMY Antoine";</description></item>
/// <item><description>a commune follows its postal code ("51500 Taissy") or starts a dated line
/// ("Reims, le 12 octobre");</description></item>
/// <item><description>a country stands alone;</description></item>
/// <item><description>a company ends or starts with a legal form, such as "Reims Auto SARL".</description></item>
/// </list>
/// </remarks>
public static class NameRecognizer
{
    /// <summary>
    /// The maximum number of words of a person name, civility aside.
    /// </summary>
    public const int MaxNameWords = 4;

    private const int MaxCompanyLength = 60;
    private const int MaxCompanyWords = 8;
    private const int MaxCountryLength = 40;

    private static readonly SearchValues<char> EndOfPlaceChars = SearchValues.Create(",;(/|");

    private static readonly char[] TrimmedChars = { ' ', '\t', ',', '.', ';', ':', '!', '?', '<', '>', '"', '(', ')', '[', ']', '«', '»', '-', '–' };

    // First names that are also common words: they need a civility or a last name in capitals
    private static readonly HashSet<string> AmbiguousFirstNames = new(StringComparer.Ordinal)
    {
        "AIME", "AMOUR", "ANGE", "AURORE", "AVRIL", "BELLE", "BLANCHE", "CERISE", "ESPOIR", "FLEUR", "FRANCE", "GRACE",
        "HOPE", "JOY", "JUSTE", "MAI", "MARINE", "NOEL", "OLIVE", "PATIENCE", "PERLE", "PRINCE", "ROSE", "VICTOIRE",
    };

    private static readonly HashSet<string> Particles = new(StringComparer.Ordinal)
    {
        "AL", "BEN", "BIN", "D", "DA", "DE", "DEL", "DEN", "DER", "DES", "DI", "DOS", "DU", "EL", "LA", "LE", "VAN", "VON",
    };

    private static readonly HashSet<string> Civilities = new(StringComparer.Ordinal)
    {
        "DOCTEUR", "DR", "M", "MADAME", "MADEMOISELLE", "MAITRE", "ME", "MISS", "MLLE", "MME", "MONSIEUR", "MR", "MRS", "MS",
        "PR", "PROFESSEUR",
    };

    // Words of organizations and places, which a person name never contains
    private static readonly HashSet<string> NotPersonWords = new(StringComparer.Ordinal)
    {
        "AGENCE", "ALLEE", "ASSOCIATION", "ASSURANCE", "ASSURANCES", "ATELIER", "AUTO", "AUTOMOBILES", "AVENUE", "BANK",
        "BANQUE", "BOULANGERIE", "BOULEVARD", "BOUTIQUE", "CABINET", "CENTRE", "CHEMIN", "CLINIQUE", "CLUB", "COLLEGE",
        "CONSEIL", "CONSULTING", "ECOLE", "EXPRESS", "GARAGE", "GROUP", "GROUPE", "HOPITAL", "HOTEL", "IMMOBILIER",
        "IMPASSE", "INFORMATIQUE", "LYCEE", "MAGASIN", "MAIRIE", "MUTUELLE", "PHARMACIE", "PLACE", "QUAI", "RESTAURANT",
        "ROUTE", "RUE", "SERVICES", "SOLUTIONS", "STUDIO", "TECHNOLOGIES", "TELEVISION", "TELEVISIONS", "TRANSPORTS",
        "UNIVERSITE",
    };

    // Compared without dots, so that "S.A.R.L." is "SARL"; case matters, so that the word "sa" is not a legal form
    private static readonly HashSet<string> LegalForms = new(StringComparer.Ordinal)
    {
        "AG", "BV", "Corp", "Corporation", "EI", "EURL", "GIE", "GmbH", "Inc", "LLC", "LLP", "Limited", "Ltd", "NV",
        "PLC", "SA", "SARL", "SAS", "SASU", "SCI", "SCOP", "SCP", "SELARL", "SELAS", "SEM", "SNC", "SRL", "SpA", "plc",
    };

    /// <summary>
    /// Recognizes the person, commune, country or company a whole span of text is, such as a line or the value of a
    /// "label: value" pair.
    /// </summary>
    /// <param name="span">The text, a whole line or a whole value.</param>
    /// <param name="label">The label of the span, given to the pieces found.</param>
    /// <param name="data">The official lists.</param>
    /// <returns>The pieces recognized: full name, first name and last name for a person, or a single piece.</returns>
    public static IReadOnlyList<TextPiece> RecognizeSpan(string span, string label, ReferenceData data)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(data);

        var datedCity = CityStartingDatedLine(span);
        if (datedCity is not null && !datedCity.Any(char.IsAsciiDigit) && data.IsCommune(datedCity))
        {
            return new[] { Piece(datedCity, CandidateKind.City, label) };
        }

        var text = span.Trim(TrimmedChars);
        if (text.Length == 0 || text.Any(char.IsAsciiDigit))
        {
            return Array.Empty<TextPiece>();
        }

        if (text.Length <= MaxCountryLength && data.IsCountry(text))
        {
            return new[] { Piece(text, CandidateKind.Country, label) };
        }

        if (IsCompany(text))
        {
            return new[] { Piece(text, CandidateKind.Company, label) };
        }

        if (TryReadPerson(text, data, out var person))
        {
            return new[]
            {
                Piece(person.FullName, CandidateKind.FullName, label),
                Piece(person.FirstName, CandidateKind.FirstName, label),
                Piece(person.LastName, CandidateKind.LastName, label),
            };
        }

        // A commune alone on its line is not recognized: too many headings and words ("FACTURE", "VILLE") are also
        // names of places. Communes are recognized next to their postal code or starting a dated line.
        return Array.Empty<TextPiece>();
    }

    /// <summary>
    /// Finds the commune written just after or just before a postal code, as in "51500 Taissy" or "Taissy 51500".
    /// </summary>
    /// <param name="before">The text before the postal code on its line.</param>
    /// <param name="postalCode">The postal code.</param>
    /// <param name="after">The text after the postal code on its line.</param>
    /// <param name="data">The official lists.</param>
    /// <returns>The commune as written, without any "Cedex" mention, or <see langword="null"/> if none goes with the postal code.</returns>
    public static string? CommuneNextToPostalCode(string before, string postalCode, string after, ReferenceData data)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(postalCode);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(data);

        // After: "51500 Taissy, France" or "51100 REIMS CEDEX 2"; the longest run of words that is a commune wins
        var afterWords = UpToPunctuation(after).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var count = afterWords.Length; count > 0; count--)
        {
            var name = string.Join(' ', afterWords.Take(count));
            if (data.IsCommuneOf(postalCode, name))
            {
                return WithoutCedex(name);
            }
        }

        var beforeWords = before.Trim(TrimmedChars).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var count = beforeWords.Length; count > 0; count--)
        {
            var name = string.Join(' ', beforeWords.Skip(beforeWords.Length - count));
            if (data.IsCommuneOf(postalCode, name))
            {
                return name;
            }
        }

        return null;
    }

    private static bool TryReadPerson(string text, ReferenceData data, out (string FullName, string FirstName, string LastName) person)
    {
        person = default;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        var hasCivility = words.Count > 0 && Civilities.Contains(ReferenceText.Normalize(words[0]));
        if (hasCivility)
        {
            words.RemoveAt(0);
        }

        if (words.Count < 2 || words.Count > MaxNameWords || !words.TrueForAll(IsNameWord))
        {
            return false;
        }

        // Particles ("de", "van") are allowed inside, every other word starts with a capital
        for (var index = 0; index < words.Count; index++)
        {
            var isParticle = Particles.Contains(ReferenceText.Normalize(words[index]));
            var isInside = index > 0 && index < words.Count - 1;
            if (!StartsLikeName(words[index]) && !(isParticle && isInside))
            {
                return false;
            }

            if (NotPersonWords.Contains(ReferenceText.Normalize(words[index])))
            {
                return false;
            }
        }

        var firstIsFirstName = data.IsFirstName(words[0]);
        var lastIsFirstName = data.IsFirstName(words[^1]);
        var firstInCapitals = IsInCapitals(words[0]);

        // "BARTHÉLÉMY Antoine": last name first, in capitals, as in official documents
        // The capitals mark the last name even when it is also a first name: "MARTIN Jean Pierre", "DE LA FONTAINE Jean"
        var capitalWords = words.TakeWhile(IsInCapitals).Count();
        var isLastNameFirst = lastIsFirstName && firstInCapitals && capitalWords < words.Count
            && !words.Skip(capitalWords).Any(IsInCapitals);
        if (!isLastNameFirst && !firstIsFirstName)
        {
            return false;
        }

        var firstNameCount = isLastNameFirst ? words.Count - capitalWords : CountFirstNames(words, data);
        var firstNameWords = isLastNameFirst ? words.Skip(words.Count - firstNameCount).ToList() : words.Take(firstNameCount).ToList();
        var lastNameWords = isLastNameFirst ? words.Take(words.Count - firstNameCount).ToList() : words.Skip(firstNameCount).ToList();
        var lastNameInCapitals = lastNameWords.Any(IsInCapitals);
        if (AmbiguousFirstNames.Contains(ReferenceText.Normalize(firstNameWords[0])) && !hasCivility && !lastNameInCapitals)
        {
            // "Rose Dupont" may be a color and a brand: only "Mme Rose Dupont" or "Rose DUPONT" are sure
            return false;
        }

        person = (string.Join(' ', words), string.Join(' ', firstNameWords), string.Join(' ', lastNameWords));
        return true;
    }

    /// <summary>
    /// Counts the first names at the start of a list of words, always leaving at least one word for the last name.
    /// </summary>
    /// <param name="words">The words, starting with the first names.</param>
    /// <param name="data">The official lists.</param>
    /// <returns>The number of first names, at least 1.</returns>
    private static int CountFirstNames(List<string> words, ReferenceData data)
    {
        var count = 1;
        while (count < words.Count - 1 && data.IsFirstName(words[count]))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Determines whether a word starts like a name: with a capital, or with a particle and an apostrophe such as "d'Arcy".
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> if the word starts like a name; otherwise <see langword="false"/>.</returns>
    private static bool StartsLikeName(string word)
    {
        return char.IsUpper(word[0]) || (word.Length > 2 && word[1] is '\'' or '’' && char.IsUpper(word[2]));
    }

    private static bool IsNameWord(string word)
    {
        return word.Any(char.IsLetter) && word.All(character => char.IsLetter(character) || character is '-' or '\'' or '’');
    }

    private static bool IsInCapitals(string text)
    {
        return text.Count(char.IsLetter) >= 2 && !text.Any(char.IsLower);
    }

    private static bool IsCompany(string text)
    {
        if (text.Length > MaxCompanyLength || text.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length > MaxCompanyWords)
        {
            return false;
        }

        // The legal form starts or ends the name: "SARL Reims Auto", "Reims Auto SARL", "Acme Inc."
        return IsLegalForm(words[0]) || IsLegalForm(words[^1]);
    }

    private static bool IsLegalForm(string word)
    {
        return LegalForms.Contains(word.Trim(TrimmedChars).Replace(".", string.Empty, StringComparison.Ordinal));
    }

    /// <summary>
    /// Finds the commune of a dated line such as "Reims, le 12 octobre 2026".
    /// </summary>
    /// <param name="span">The line.</param>
    /// <returns>The text before ", le", or <see langword="null"/> if the line is not dated this way.</returns>
    private static string? CityStartingDatedLine(string span)
    {
        var comma = span.IndexOf(", le ", StringComparison.OrdinalIgnoreCase);
        return comma > 0 ? span[..comma].Trim() : null;
    }

    private static string UpToPunctuation(string text)
    {
        var end = text.AsSpan().IndexOfAny(EndOfPlaceChars);
        return (end >= 0 ? text[..end] : text).Trim(TrimmedChars);
    }

    private static string WithoutCedex(string name)
    {
        var cedex = name.IndexOf(" cedex", StringComparison.OrdinalIgnoreCase);
        return cedex > 0 ? name[..cedex] : name;
    }

    private static TextPiece Piece(string value, CandidateKind kind, string label)
    {
        return new TextPiece { Value = value, Kind = kind, Label = label };
    }
}
