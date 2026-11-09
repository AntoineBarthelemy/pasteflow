using System.Text.RegularExpressions;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Matching;

/// <summary>
/// Chooses the option of a list, of radio buttons or of a tick box from what the screen says, only when it is clear:
/// one option of the list is written in a value that talks about the field, and no other option is.
/// </summary>
/// <remarks>
/// <para>« Type de logement » with « Maison » and « Appartement », next to « Logement : appartement de 3 pièces » on
/// screen, takes « Appartement ». When two options are written, the field is left with both proposed.</para>
/// <para>A tick box is ticked only on a clear yes, such as « Logement meublé : oui »; « non meublé » leaves it as it is.</para>
/// </remarks>
public static partial class ChoiceMatcher
{
    /// <summary>
    /// The value written in a tick box to tick it.
    /// </summary>
    public const string Ticked = "oui";

    private const double StrongLabel = 0.5;

    private static readonly HashSet<string> YesWords = new(StringComparer.Ordinal) { "OUI", "YES", "X", "VRAI", "TRUE" };

    private static readonly Dictionary<char, HashSet<string>> OptionsOfSex = new()
    {
        ['F'] = new(StringComparer.Ordinal) { "FEMININ", "FEMME", "F", "MADAME", "MME", "FEMALE", "WOMAN" },
        ['M'] = new(StringComparer.Ordinal) { "MASCULIN", "HOMME", "H", "M", "MONSIEUR", "MALE", "MAN" },
    };

    /// <summary>
    /// Chooses the option for a field.
    /// </summary>
    /// <param name="field">A list, a group of radio buttons or a tick box.</param>
    /// <param name="candidates">The values found on screen.</param>
    /// <returns>The option to choose and whether it is sure; no candidate when nothing on screen names an option.</returns>
    public static FieldMatch Match(FormField field, IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(candidates);

        var clues = FieldClues.Of(field);

        // The values that talk about the field: their label shares its words, or they are of the kind it expects
        var related = candidates
            .Where(candidate => !candidate.IsFromHistory && !candidate.IsLinkAddress)
            .Where(candidate => clues.Similarity(LabelConcepts.From(candidate.Label)) > 0
                || (candidate.Kind != CandidateKind.Text && clues.ExpectedKinds.Contains(candidate.Kind)))
            .ToList();

        if (field.Kind == FieldKind.CheckBox)
        {
            return MatchTickBox(field, related);
        }

        // « Sexe » or « Civilité » with « Mme Camille Rousseau » on screen: the title says it, when every title agrees
        if (clues.Mentions(LabelConcepts.Sex) && field.Options.Count > 0 && SexOf(candidates) is { } sex
            && field.Options.Where(option => OptionsOfSex[sex].Contains(ReferenceText.Normalize(option))).ToList() is { Count: 1 } options)
        {
            var source = candidates.First(candidate => !candidate.IsFromHistory && !candidate.IsFromProfile && TitleRegex().IsMatch(candidate.Value));
            return new FieldMatch
            {
                Field = field,
                Candidate = source with { Value = options[0], Kind = CandidateKind.Text },
                Score = 1,
                IsConfident = true,
                Suggestions = new List<Candidate>(),
            };
        }

        return field.Options.Count > 0 ? MatchOption(field, related) : MatchUnknownOption(field, clues, related);
    }

    // A closed list of a web page does not show its options until opened: the only recognized value of the kind the
    // field expects, such as a country for « Pays de livraison », is chosen; the list itself says whether it has it
    private static FieldMatch MatchUnknownOption(FormField field, FieldClues clues, List<Candidate> related)
    {
        // A value about the field may hold the one expected: « Adresse de livraison : Rue Haute 41, 1000 Bruxelles,
        // Belgique » holds the country
        var held = related
            .Where(candidate => candidate.Kind == CandidateKind.Text)
            .SelectMany(candidate => candidate.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .SelectMany(part => TextSegmenter.Segment(part))
                .Where(piece => piece.Kind != CandidateKind.Text)
                .Select(piece => candidate with { Value = piece.Value, Kind = piece.Kind }));
        var values = related
            .Concat(held)
            .Where(candidate => candidate.Kind != CandidateKind.Text && clues.ExpectedKinds.Contains(candidate.Kind))
            .GroupBy(candidate => ReferenceText.Normalize(candidate.Value), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (values.Count == 0 && clues.ExpectedKinds.Count == 0)
        {
            // « Nature du contrat » next to « Contrat : CDD de 8 mois » : the short word in capitals that opens the value
            // is tried in the list, which refuses it if it has no such option
            values = related
                .Where(candidate => candidate.Kind == CandidateKind.Text && clues.Similarity(LabelConcepts.From(candidate.Label)) >= StrongLabel)
                .Select(candidate => candidate with { Value = OpeningAcronymRegex().Match(candidate.Value).Value })
                .Where(candidate => candidate.Value.Length > 0)
                .GroupBy(candidate => candidate.Value, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
        }

        if (values.Count == 0)
        {
            return new FieldMatch { Field = field, Reason = MatchReasons.NothingFound };
        }

        return new FieldMatch
        {
            Field = field,
            Candidate = values[0],
            Score = values.Count == 1 ? 1 : 0.5,
            IsConfident = values.Count == 1,
            Suggestions = values,
            Reason = values.Count == 1 ? string.Empty : MatchReasons.Hesitation(values[0], values[1]),
        };
    }

    private static FieldMatch MatchOption(FormField field, List<Candidate> related)
    {
        var found = new List<(string Option, Candidate Candidate)>();
        foreach (var option in field.Options.Where(option => ReferenceText.Normalize(option).Length > 0))
        {
            var words = $" {ReferenceText.Normalize(option)} ";
            var candidate = related.FirstOrDefault(candidate => $" {ReferenceText.Normalize(candidate.Value)} ".Contains(words, StringComparison.Ordinal));
            if (candidate is not null)
            {
                found.Add((option, candidate));
            }
        }

        if (found.Count == 0)
        {
            return new FieldMatch { Field = field, Reason = MatchReasons.NothingFound };
        }

        var suggestions = found.Select(item => item.Candidate with { Value = item.Option, Kind = CandidateKind.Text }).ToList();
        return new FieldMatch
        {
            Field = field,
            Candidate = suggestions[0],
            Score = found.Count == 1 ? 1 : 0.5,
            IsConfident = found.Count == 1,
            Suggestions = suggestions,
            Reason = found.Count == 1 ? string.Empty : MatchReasons.Hesitation(suggestions[0], suggestions[1]),
        };
    }

    // The sex said by every title on screen, or nothing when there is none or when they disagree
    private static char? SexOf(IReadOnlyList<Candidate> candidates)
    {
        var sexes = candidates
            .Where(candidate => !candidate.IsFromHistory && !candidate.IsFromProfile)
            .SelectMany(candidate => TitleRegex().Matches(candidate.Value))
            .Select(title => title.Groups["female"].Success ? 'F' : 'M')
            .Distinct()
            .ToList();
        return sexes.Count == 1 ? sexes[0] : null;
    }

    private static FieldMatch MatchTickBox(FormField field, List<Candidate> related)
    {
        var yes = related.FirstOrDefault(candidate => YesWords.Contains(ReferenceText.Normalize(candidate.Value)));
        return yes is null
            ? new FieldMatch { Field = field, Reason = MatchReasons.NothingFound }
            : new FieldMatch
            {
                Field = field,
                Candidate = yes with { Value = Ticked, Kind = CandidateKind.Text },
                Score = 1,
                IsConfident = true,
                Suggestions = new List<Candidate>(),
            };
    }

    [GeneratedRegex(@"\b(?:(?<female>Mme|Madame|Mlle|Mademoiselle)|(?:M\.|Monsieur|Mr\.?))\s+\p{Lu}", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^\p{Lu}{2,6}(?=$|[\s,;.])", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex OpeningAcronymRegex();
}
