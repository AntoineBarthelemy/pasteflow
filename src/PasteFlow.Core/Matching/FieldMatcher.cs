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
    /// The minimum score to fill a field without asking.
    /// </summary>
    public const double ConfidenceThreshold = 0.6;

    /// <summary>
    /// The minimum lead of the best value over the second best one to fill a field without asking.
    /// </summary>
    public const double MinimumLead = 0.15;

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
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(candidates);

        var clues = FieldClues.Of(field);
        var distinctValuesByKind = candidates
            .GroupBy(candidate => candidate.Kind)
            .ToDictionary(group => group.Key, group => group.Select(candidate => candidate.Value).Distinct(StringComparer.Ordinal).Count());

        // A value found several times keeps its best score: it is one choice, not several
        var ranked = candidates
            .Select(candidate => (Candidate: candidate, Score: Score(candidate, clues, distinctValuesByKind)))
            .Where(scored => scored.Score > 0)
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
            IsConfident = best.Score >= ConfidenceThreshold && lead >= MinimumLead,
        };
    }

    private static double Score(
        Candidate candidate,
        FieldClues clues,
        Dictionary<CandidateKind, int> distinctValuesByKind)
    {
        var expectedKinds = clues.ExpectedKinds;
        double score;
        if (expectedKinds.Count == 0)
        {
            // No clue about the expected value: only the labels can tell
            score = TextKindScore;
        }
        else if (!expectedKinds.Contains(candidate.Kind))
        {
            return 0;
        }
        else if (candidate.Kind == CandidateKind.Text)
        {
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
