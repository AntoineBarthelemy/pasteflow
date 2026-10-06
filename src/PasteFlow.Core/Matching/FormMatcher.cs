using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Matching;

/// <summary>
/// Chooses the values of a whole form, so that the same value is never pasted into two fields.
/// </summary>
/// <remarks>
/// <para>Each field is first matched on its own by <see cref="FieldMatcher"/>. The fields are then served from the
/// surest to the least sure: a field takes its value only if no surer field took it before. A field whose best
/// value is taken may still get another value, only if that value fits it on its own merits.</para>
/// <para>A confirmation field, such as "Confirmez votre email", is the exception: it is meant to receive the same
/// value as the field it confirms.</para>
/// </remarks>
public static class FormMatcher
{
    /// <summary>
    /// Finds the values of the fields of a form.
    /// </summary>
    /// <param name="fields">The fields to fill, in the order of the form.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <returns>A match for each field, in the order of the form.</returns>
    public static IReadOnlyList<FieldMatch> MatchAll(IReadOnlyList<FormField> fields, IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(candidates);

        var firstMatches = fields.Select(field => FieldMatcher.Match(field, candidates)).ToArray();
        var matches = new FieldMatch[fields.Count];
        var takenValues = new HashSet<string>(StringComparer.Ordinal);

        // The surest fields choose first; the order of the form decides between equal scores
        var surestFirst = Enumerable.Range(0, fields.Count)
            .OrderByDescending(index => firstMatches[index].IsConfident)
            .ThenByDescending(index => firstMatches[index].Score);
        foreach (var index in surestFirst)
        {
            if (FieldClues.Of(fields[index]).IsConfirmation)
            {
                // Safety net exception: a confirmation repeats a value on purpose and takes nothing from others
                matches[index] = firstMatches[index];
                continue;
            }

            var match = takenValues.Count == 0
                ? firstMatches[index]
                : FieldMatcher.Match(fields[index], candidates, takenValues);
            if (match is { IsConfident: true, Candidate: not null })
            {
                _ = takenValues.Add(match.Candidate.Value);
            }

            matches[index] = match;
        }

        return matches;
    }
}
