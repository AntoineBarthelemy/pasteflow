using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Matching;

/// <summary>
/// Chooses, among the candidates, the value that fits a field best and tells whether it is sure enough to fill it.
/// </summary>
/// <remarks>
/// <para>Each candidate gets a score made of:</para>
/// <list type="bullet">
/// <item><description>the kind: a value of the kind expected by the field (an email for "Email") scores high,
/// a value of another recognized kind is excluded;</description></item>
/// <item><description>the labels: the more the label of the value talks about the same thing as the field
/// (its label, technical name or help text, see <see cref="FieldClues"/>), the higher;</description></item>
/// <item><description>uniqueness: the only value of the expected kind on screen is very likely the right one;</description></item>
/// <item><description>closeness: values of farther windows score slightly lower.</description></item>
/// </list>
/// <para>The field is filled only when the best value reaches <see cref="ConfidenceThreshold"/> and is ahead of
/// the second best value by at least <see cref="MinimumLead"/>: when in doubt, the field is left to the user.</para>
/// </remarks>
public static class FieldMatcher
{
    /// <summary>
    /// The minimum score to fill a field without asking, with the default prudence (see <see cref="MatchingOptions"/>).
    /// </summary>
    public const double ConfidenceThreshold = 0.6;

    /// <summary>
    /// The minimum lead of the best value over the second best one to fill a field without asking.
    /// </summary>
    public const double MinimumLead = 0.15;

    /// <summary>
    /// The minimum score to propose a value to the user when the field is not filled; a value of the expected kind
    /// reaches it even without label, a value of no particular kind does not.
    /// </summary>
    public const double SuggestionThreshold = 0.4;

    /// <summary>
    /// The maximum number of values proposed for a field.
    /// </summary>
    public const int MaxSuggestions = 3;

    private const double ExpectedKindScore = 0.5;
    private const double TextKindScore = 0.2;
    private const double LabelWeight = 0.5;
    private const double UniqueKindBonus = 0.2;
    private const double RankPenalty = 0.03;

    /// <summary>
    /// Finds the best value for a field.
    /// </summary>
    /// <param name="field">The field to fill.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <returns>The best candidate and whether it is confident enough.</returns>
    public static FieldMatch Match(FormField field, IReadOnlyList<Candidate> candidates)
    {
        return Match(field, candidates, new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Finds the best value for a field, among the values not already taken by other fields.
    /// </summary>
    /// <param name="field">The field to fill.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <param name="takenValues">
    /// The values already given to other fields: they are never chosen, but they still count when telling whether
    /// a value is the only one of its kind, so that a field is never filled by elimination.
    /// </param>
    /// <returns>The best candidate and whether it is confident enough.</returns>
    public static FieldMatch Match(FormField field, IReadOnlyList<Candidate> candidates, IReadOnlySet<string> takenValues)
    {
        ArgumentNullException.ThrowIfNull(field);

        return Match(field, FieldClues.Of(field), candidates, takenValues);
    }

    /// <summary>
    /// Finds the best value for a field whose clues are already gathered, among the values not taken by other fields.
    /// </summary>
    /// <param name="field">The field to fill.</param>
    /// <param name="clues">What the field tells about the value it expects, knowing the rest of the form.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <param name="takenValues">The values already given to other fields, see <see cref="Match(FormField, IReadOnlyList{Candidate}, IReadOnlySet{string})"/>.</param>
    /// <returns>The best candidate and whether it is confident enough.</returns>
    public static FieldMatch Match(FormField field, FieldClues clues, IReadOnlyList<Candidate> candidates, IReadOnlySet<string> takenValues)
    {
        return Match(field, clues, candidates, takenValues, MatchingOptions.Balanced);
    }

    /// <summary>
    /// Finds the best value for a field whose clues are already gathered, with given thresholds of confidence.
    /// </summary>
    /// <param name="field">The field to fill.</param>
    /// <param name="clues">What the field tells about the value it expects, knowing the rest of the form.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <param name="takenValues">The values already given to other fields, see <see cref="Match(FormField, IReadOnlyList{Candidate}, IReadOnlySet{string})"/>.</param>
    /// <param name="options">The thresholds that decide whether the best value is sure enough.</param>
    /// <returns>The best candidate and whether it is confident enough.</returns>
    public static FieldMatch Match(
        FormField field,
        FieldClues clues,
        IReadOnlyList<Candidate> candidates,
        IReadOnlySet<string> takenValues,
        MatchingOptions options)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(clues);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(takenValues);
        ArgumentNullException.ThrowIfNull(options);

        // Only the values the field can accept compete, so "the only value of its kind" means the only acceptable one
        var eligible = candidates.Where(candidate => IsEligible(candidate, clues)).ToList();
        var distinctValuesByKind = eligible
            .GroupBy(candidate => candidate.Kind)
            .ToDictionary(group => group.Key, group => group.Select(candidate => candidate.Value).Distinct(StringComparer.Ordinal).Count());

        // A value found several times keeps its best score: it is one choice, not several
        var ranked = eligible
            .Select(candidate => (Candidate: candidate, Score: Score(candidate, clues, distinctValuesByKind)))
            .Where(scored => scored.Score > 0 && !takenValues.Contains(scored.Candidate.Value))
            .GroupBy(scored => scored.Candidate.Value, StringComparer.Ordinal)
            .Select(group => group.MaxBy(scored => scored.Score))
            .OrderByDescending(scored => scored.Score)
            .ToList();

        if (ranked.Count == 0)
        {
            return new FieldMatch { Field = field };
        }

        var best = ranked[0];
        var lead = ranked.Count > 1 ? best.Score - ranked[1].Score : double.MaxValue;

        return new FieldMatch
        {
            Field = field,
            Candidate = best.Candidate,
            Score = best.Score,
            IsConfident = best.Score >= options.ConfidenceThreshold && lead >= options.MinimumLead,
            Suggestions = ranked
                .Where(scored => scored.Score >= SuggestionThreshold)
                .Take(MaxSuggestions)
                .Select(scored => scored.Candidate)
                .ToList(),
        };
    }

    /// <summary>
    /// Determines whether a field can accept a candidate at all, whatever its score.
    /// </summary>
    /// <param name="candidate">The candidate to check.</param>
    /// <param name="clues">The clues of the field.</param>
    /// <returns><see langword="true"/> if the candidate may compete for the field; otherwise <see langword="false"/>.</returns>
    private static bool IsEligible(Candidate candidate, FieldClues clues)
    {
        var expectedKinds = clues.ExpectedKinds;
        if (expectedKinds.Count > 0 && !expectedKinds.Contains(candidate.Kind))
        {
            return false;
        }

        // A last name field does not take a whole name read as plain text, such as "Nom : Wei Zhang"
        if (candidate.Kind == CandidateKind.Text && expectedKinds.Contains(CandidateKind.LastName)
            && candidate.Value.Trim().Contains(' ', StringComparison.Ordinal)
            && !LabelConcepts.From(candidate.Label).Contains(LabelConcepts.FamilyName))
        {
            return false;
        }

        // Safety net: a postal code field only takes a real French postal code
        return candidate.Kind != CandidateKind.Number
            || !clues.Mentions(LabelConcepts.PostalCode)
            || ValueValidator.IsFrenchPostalCode(candidate.Value);
    }

    private static double Score(
        Candidate candidate,
        FieldClues clues,
        Dictionary<CandidateKind, int> distinctValuesByKind)
    {
        double score;
        if (clues.ExpectedKinds.Count == 0 || candidate.Kind == CandidateKind.Text)
        {
            // No clue about the expected value, or plain text: only the labels can tell
            score = TextKindScore;
        }
        else
        {
            score = ExpectedKindScore;
            if (distinctValuesByKind[candidate.Kind] == 1)
            {
                score += UniqueKindBonus;
            }
        }

        score += LabelWeight * clues.Similarity(LabelConcepts.From(candidate.Label));
        score -= RankPenalty * candidate.SourceRank;

        // Without matching label, a plain text is just a line of the screen, never a sure value
        return candidate.Kind == CandidateKind.Text && score <= TextKindScore ? 0 : Math.Max(score, 0);
    }
}
