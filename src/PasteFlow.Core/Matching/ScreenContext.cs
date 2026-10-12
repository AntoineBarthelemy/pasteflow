using System.Text.RegularExpressions;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Matching;

/// <summary>
/// What the window being filled already says, used to tell apart values of the same kind, as a person would:
/// in a message starting with "Bonjour Léa", the email to paste is Léa's, not Hugo's.
/// </summary>
/// <remarks>
/// <para>Each value is tied to the words around it: its own words ("lea.martin@atelier-nova.fr" gives LEA, MARTIN,
/// ATELIER, NOVA), its label, and the line of the person or company it belongs to (see
/// <see cref="Candidate.Context"/>). Only the words that tie a value to nothing else count: ATELIER and NOVA,
/// shared by Léa's and Hugo's emails, tell nothing apart.</para>
/// <para>When the window being filled mentions such a word for one value only, that value is the one meant.
/// The person greeted ("Bonjour Léa") is looked at first, then the rest of the window. A clue never decides alone:
/// it only counts when several values of the same recognized kind compete, and when it points to one of them,
/// never two.</para>
/// </remarks>
public sealed partial class ScreenContext
{
    private const int MinWordLength = 3;
    private const int RegexTimeoutMilliseconds = 1000;

    // Words found in so many emails, greetings or signatures that they tie a value to nobody
    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "COM", "NET", "ORG", "WWW", "HTTP", "HTTPS", "GMAIL", "HOTMAIL", "OUTLOOK", "YAHOO", "ORANGE", "FREE",
        "WANADOO", "LAPOSTE", "ICLOUD", "LIVE", "MSN", "SFR", "MAIL", "EMAIL", "CONTACT", "INFO", "BONJOUR", "HELLO",
        "MERCI", "CORDIALEMENT", "MONSIEUR", "MADAME", "LES", "DES", "POUR", "AVEC", "DANS", "THE", "AND",
    };

    private readonly string[] _texts;
    private readonly string[] _greeted;

    private ScreenContext(string[] texts, string[] greeted)
    {
        _texts = texts;
        _greeted = greeted;
    }

    /// <summary>
    /// Gets a context that says nothing, for matching without knowing the window being filled.
    /// </summary>
    public static ScreenContext Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>());

    /// <summary>
    /// Gathers what the window being filled says, from the values read in it (source rank 0).
    /// </summary>
    /// <param name="candidates">The values found on screen.</param>
    /// <returns>The context of the window being filled.</returns>
    public static ScreenContext From(IEnumerable<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var rawTexts = candidates
            .Where(candidate => candidate.SourceRank == 0 && !candidate.IsFromHistory && !candidate.IsFromProfile)
            .Select(candidate => $"{candidate.Label} {candidate.Value}")
            .ToList();
        var texts = rawTexts
            .Select(ReferenceText.Normalize)
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var greeted = rawTexts
            .SelectMany(text => GreetingRegex().Matches(ReferenceText.RemoveAccents(text)))
            .Select(match => ReferenceText.Normalize(match.Groups["greeted"].Value))
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new ScreenContext(texts, greeted);
    }

    /// <summary>
    /// Cuts a text into the words that can tie a value to a person, a company or a subject.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The words, in upper case without accents, without short and common words.</returns>
    public static HashSet<string> Words(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return ReferenceText.Normalize(text)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= MinWordLength && !CommonWords.Contains(word))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds, among values competing for a field, the ones the window being filled points to.
    /// </summary>
    /// <param name="competitors">The values the field can accept.</param>
    /// <returns>
    /// For each recognized kind with several values, the only value the context points to, if there is exactly one.
    /// </returns>
    public IReadOnlySet<string> ChooseAmong(IReadOnlyList<Candidate> competitors)
    {
        ArgumentNullException.ThrowIfNull(competitors);

        var chosen = new HashSet<string>(StringComparer.Ordinal);
        if (_texts.Length == 0)
        {
            return chosen;
        }

        var recognized = competitors.Where(candidate => candidate.Kind != CandidateKind.Text);
        foreach (var kindGroup in recognized.GroupBy(candidate => candidate.Kind))
        {
            var wordsByValue = kindGroup
                .GroupBy(candidate => candidate.Value, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.SelectMany(TiedWords).ToHashSet(StringComparer.Ordinal),
                    StringComparer.Ordinal);
            if (wordsByValue.Count < 2)
            {
                continue;
            }

            // The person greeted is the surest clue; the rest of the window only counts when no one is greeted
            var pointedValues = Pointed(wordsByValue, _greeted.SelectMany(Words).ToHashSet(StringComparer.Ordinal));
            if (pointedValues.Count == 0)
            {
                pointedValues = Pointed(wordsByValue, CluesBesides(wordsByValue.Keys));
            }

            if (pointedValues.Count == 1)
            {
                _ = chosen.Add(pointedValues[0]);
            }
        }

        return chosen;
    }

    private static List<string> Pointed(Dictionary<string, HashSet<string>> wordsByValue, HashSet<string> clues)
    {
        return wordsByValue
            .Where(entry => OwnWords(entry.Key, wordsByValue).Overlaps(clues))
            .Select(entry => entry.Key)
            .ToList();
    }

    private static IEnumerable<string> TiedWords(Candidate candidate)
    {
        return Words($"{candidate.Value} {candidate.Label} {candidate.Context}");
    }

    /// <summary>
    /// Keeps the words tied to one value only: a word shared with another value tells nothing apart.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="wordsByValue">The words tied to each competing value.</param>
    /// <returns>The words of this value that no other value has.</returns>
    private static HashSet<string> OwnWords(string value, Dictionary<string, HashSet<string>> wordsByValue)
    {
        var own = new HashSet<string>(wordsByValue[value], StringComparer.Ordinal);
        foreach (var (otherValue, otherWords) in wordsByValue)
        {
            if (!string.Equals(otherValue, value, StringComparison.Ordinal))
            {
                own.ExceptWith(otherWords);
            }
        }

        return own;
    }

    /// <summary>
    /// Finds a greeting and the few words that follow it, up to the first punctuation: "Bonjour Léa," gives "Léa".
    /// </summary>
    [GeneratedRegex(
        @"\b(?:bonjour|bonsoir|salut|coucou|hello|hi|hey|dear|cher|chere|chers|cheres)\s+(?<greeted>[^,.;:!?\r\n]{1,60})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RegexTimeoutMilliseconds)]
    private static partial Regex GreetingRegex();

    /// <summary>
    /// Gathers the words of the window being filled, leaving out the texts that hold one of the competing values:
    /// a value shown in the window would otherwise point to itself.
    /// </summary>
    /// <param name="values">The competing values.</param>
    /// <returns>The words that may point to a value.</returns>
    private HashSet<string> CluesBesides(IEnumerable<string> values)
    {
        var normalizedValues = values.Select(ReferenceText.Normalize).Where(value => value.Length > 0).ToList();
        return _texts
            .Where(text => !normalizedValues.Exists(value => text.Contains(value, StringComparison.Ordinal)))
            .SelectMany(Words)
            .ToHashSet(StringComparer.Ordinal);
    }
}
