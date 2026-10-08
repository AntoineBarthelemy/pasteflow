using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Reference;

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
/// <para>The values of the screen come first. The values of the history only fill the fields the screen offers
/// nothing plausible for; elsewhere they are only proposed.</para>
/// <para>Last safety net: a postal code and a commune that do not go together according to La Poste are both left
/// to the user.</para>
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
        return MatchAll(fields, candidates, ReferenceData.Shared);
    }

    /// <summary>
    /// Finds the values of the fields of a form, checking places against given official lists.
    /// </summary>
    /// <param name="fields">The fields to fill, in the order of the form.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <param name="data">The official lists used to check that a postal code and a commune go together.</param>
    /// <returns>A match for each field, in the order of the form.</returns>
    public static IReadOnlyList<FieldMatch> MatchAll(IReadOnlyList<FormField> fields, IReadOnlyList<Candidate> candidates, ReferenceData data)
    {
        return MatchAll(fields, candidates, data, MatchingOptions.Balanced);
    }

    /// <summary>
    /// Finds the values of the fields of a form, with given official lists and thresholds of confidence.
    /// </summary>
    /// <param name="fields">The fields to fill, in the order of the form.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <param name="data">The official lists used to check that a postal code and a commune go together.</param>
    /// <param name="options">The thresholds that decide whether a value is sure enough, set by the prudence chosen.</param>
    /// <returns>A match for each field, in the order of the form.</returns>
    public static IReadOnlyList<FieldMatch> MatchAll(
        IReadOnlyList<FormField> fields,
        IReadOnlyList<Candidate> candidates,
        ReferenceData data,
        MatchingOptions options)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);

        // "Nom" means the last name when the form asks the first name elsewhere, the whole name otherwise
        var firstNameAskedApart = fields.Any(field => AsksFirstNameOnly(FieldClues.Of(field)));
        var clues = fields.Select(field => FieldClues.Of(field, firstNameAskedApart)).ToArray();

        // The screen comes first: the history only fills the fields the screen says nothing about
        var screenCandidates = candidates.Where(candidate => !candidate.IsFromHistory).ToList();
        var historyCandidates = candidates.Where(candidate => candidate.IsFromHistory).ToList();
        var noTakenValue = new HashSet<string>(StringComparer.Ordinal);
        var firstMatches = fields.Select((field, index) => FieldMatcher.Match(field, clues[index], screenCandidates, noTakenValue, options)).ToArray();
        var matches = new FieldMatch[fields.Count];
        var takenValues = new HashSet<string>(StringComparer.Ordinal);

        // The surest fields choose first; the order of the form decides between equal scores
        var surestFirst = Enumerable.Range(0, fields.Count)
            .OrderByDescending(index => firstMatches[index].IsConfident)
            .ThenByDescending(index => firstMatches[index].Score);
        foreach (var index in surestFirst)
        {
            if (clues[index].IsConfirmation)
            {
                // Safety net exception: a confirmation repeats a value on purpose and takes nothing from others
                matches[index] = firstMatches[index];
                continue;
            }

            var match = takenValues.Count == 0
                ? firstMatches[index]
                : FieldMatcher.Match(fields[index], clues[index], screenCandidates, takenValues, options);
            if (match is { IsConfident: true, Candidate: not null })
            {
                _ = takenValues.Add(match.Candidate.Value);
            }

            matches[index] = match;
        }

        if (historyCandidates.Count > 0)
        {
            FillGapsFromHistory(fields, clues, matches, historyCandidates, takenValues, options);
        }

        CheckPostalCodeAndCommune(matches, clues, data);
        return matches;
    }

    /// <summary>
    /// Fills from the history the fields the screen offered nothing plausible for, and adds the values of the history
    /// to the suggestions of the other fields left to the user.
    /// </summary>
    /// <param name="fields">The fields of the form.</param>
    /// <param name="clues">The clues of each field.</param>
    /// <param name="matches">The matches found on screen, changed in place.</param>
    /// <param name="historyCandidates">The values of the history.</param>
    /// <param name="takenValues">The values already given to fields, completed in place.</param>
    /// <param name="options">The thresholds of confidence.</param>
    private static void FillGapsFromHistory(
        IReadOnlyList<FormField> fields,
        FieldClues[] clues,
        FieldMatch[] matches,
        List<Candidate> historyCandidates,
        HashSet<string> takenValues,
        MatchingOptions options)
    {
        var noTakenValue = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < matches.Length; index++)
        {
            var screenMatch = matches[index];
            if (screenMatch.IsConfident)
            {
                continue;
            }

            var isConfirmation = clues[index].IsConfirmation;
            var historyMatch = FieldMatcher.Match(fields[index], clues[index], historyCandidates, isConfirmation ? noTakenValue : takenValues, options);
            if (screenMatch.Suggestions.Count == 0 && historyMatch is { IsConfident: true, Candidate: not null })
            {
                matches[index] = historyMatch;
                if (!isConfirmation)
                {
                    _ = takenValues.Add(historyMatch.Candidate.Value);
                }

                continue;
            }

            // The screen hesitates: the history does not decide, it only proposes
            matches[index] = screenMatch with
            {
                Suggestions = screenMatch.Suggestions
                    .Concat(historyMatch.Suggestions)
                    .DistinctBy(candidate => candidate.Value, StringComparer.Ordinal)
                    .Take(FieldMatcher.MaxSuggestions)
                    .ToList(),
            };
        }
    }

    private static bool AsksFirstNameOnly(FieldClues clues)
    {
        return clues.ExpectedKinds.Contains(CandidateKind.FirstName);
    }

    /// <summary>
    /// Safety net: a postal code and a commune that do not go together according to La Poste are both left to the
    /// user, since one of them is wrong.
    /// </summary>
    /// <param name="matches">The matches of the form, changed in place.</param>
    /// <param name="clues">The clues of each field.</param>
    /// <param name="data">The official lists.</param>
    private static void CheckPostalCodeAndCommune(FieldMatch[] matches, FieldClues[] clues, ReferenceData data)
    {
        var postalCodeIndex = -1;
        var communeIndex = -1;
        for (var index = 0; index < matches.Length; index++)
        {
            if (!matches[index].IsConfident)
            {
                continue;
            }

            if (clues[index].Mentions(LabelConcepts.PostalCode))
            {
                postalCodeIndex = postalCodeIndex < 0 ? index : postalCodeIndex;
            }
            else if (clues[index].Mentions(LabelConcepts.City))
            {
                communeIndex = communeIndex < 0 ? index : communeIndex;
            }
        }

        if (postalCodeIndex < 0 || communeIndex < 0
            || matches[postalCodeIndex].Candidate is not { } postalCode
            || matches[communeIndex].Candidate is not { } commune)
        {
            return;
        }

        if (data.IsPostalCode(postalCode.Value) && data.IsCommune(commune.Value) && !data.IsCommuneOf(postalCode.Value, commune.Value))
        {
            matches[postalCodeIndex] = matches[postalCodeIndex] with { IsConfident = false };
            matches[communeIndex] = matches[communeIndex] with { IsConfident = false };
        }
    }
}
