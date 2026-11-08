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
public static class ChoiceMatcher
{
    /// <summary>
    /// The value written in a tick box to tick it.
    /// </summary>
    public const string Ticked = "oui";

    private static readonly HashSet<string> YesWords = new(StringComparer.Ordinal) { "OUI", "YES", "X", "VRAI", "TRUE" };

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

        return field.Options.Count > 0 ? MatchOption(field, related) : MatchUnknownOption(field, clues, related);
    }

    // A closed list of a web page does not show its options until opened: the only recognized value of the kind the
    // field expects, such as a country for « Pays de livraison », is chosen; the list itself says whether it has it
    private static FieldMatch MatchUnknownOption(FormField field, FieldClues clues, List<Candidate> related)
    {
        var values = related
            .Where(candidate => candidate.Kind != CandidateKind.Text && clues.ExpectedKinds.Contains(candidate.Kind))
            .GroupBy(candidate => ReferenceText.Normalize(candidate.Value), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
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
}
