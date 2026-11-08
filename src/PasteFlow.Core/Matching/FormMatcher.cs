using System.Text.RegularExpressions;
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
/// <para>The values of the screen come first. The values of the profile of the user then fill the fields that are
/// about the user, see <see cref="ProfileArbiter"/>. The values of the history only fill the fields still left with
/// nothing plausible; elsewhere they are only proposed.</para>
/// <para>A value read from the image of a window (OCR) is only proposed, never written without the user.</para>
/// <para>Last safety net: a postal code and a commune that do not go together according to La Poste are both left
/// to the user.</para>
/// </remarks>
public static partial class FormMatcher
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
        return MatchAll(fields, candidates, data, options, fields);
    }

    /// <summary>
    /// Finds the values of some fields of a form, knowing all its fields.
    /// </summary>
    /// <param name="fields">The fields to fill, in the order of the form.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <param name="data">The official lists used to check that a postal code and a commune go together.</param>
    /// <param name="options">The thresholds that decide whether a value is sure enough, set by the prudence chosen.</param>
    /// <param name="formFields">
    /// All the fields of the form, including those already filled: they tell what the other fields mean, such as a
    /// "Prénom" field already filled that makes "Nom" the last name.
    /// </param>
    /// <returns>A match for each field to fill, in the order of the form.</returns>
    public static IReadOnlyList<FieldMatch> MatchAll(
        IReadOnlyList<FormField> fields,
        IReadOnlyList<Candidate> candidates,
        ReferenceData data,
        MatchingOptions options,
        IReadOnlyList<FormField> formFields)
    {
        return MatchAll(fields, candidates, data, options, formFields, PageContext.Empty);
    }

    /// <summary>
    /// Finds the values of some fields of a form, knowing all its fields and the page that shows it.
    /// </summary>
    /// <param name="fields">The fields to fill, in the order of the form.</param>
    /// <param name="candidates">The values found in the sources.</param>
    /// <param name="data">The official lists used to check that a postal code and a commune go together.</param>
    /// <param name="options">The thresholds that decide whether a value is sure enough, set by the prudence chosen.</param>
    /// <param name="formFields">All the fields of the form, including those already filled.</param>
    /// <param name="page">
    /// What the window being filled tells: whether the form is about the user ("Créer un compte") or someone else
    /// ("Nouveau client"), and on which site, to choose among several values of the profile.
    /// </param>
    /// <returns>A match for each field to fill, in the order of the form.</returns>
    public static IReadOnlyList<FieldMatch> MatchAll(
        IReadOnlyList<FormField> fields,
        IReadOnlyList<Candidate> candidates,
        ReferenceData data,
        MatchingOptions options,
        IReadOnlyList<FormField> formFields,
        PageContext page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(formFields);

        // "Nom" means the last name when the form asks the first name elsewhere, the whole name otherwise
        var firstNameAskedApart = formFields.Concat(fields).Any(field => AsksFirstNameOnly(FieldClues.Of(field)));
        var clues = fields.Select(field => FieldClues.Of(field, firstNameAskedApart)).ToArray();

        // The screen comes first, then the profile for the fields about the user; the history only fills the gaps left
        var screenCandidates = candidates.Where(candidate => !candidate.IsFromHistory && !candidate.IsFromProfile).ToList();
        var profileCandidates = candidates.Where(candidate => candidate.IsFromProfile).ToList();
        var historyCandidates = candidates.Where(candidate => candidate.IsFromHistory).ToList();
        var noTakenValue = new HashSet<string>(StringComparer.Ordinal);
        var context = ScreenContext.From(screenCandidates);
        var firstMatches = fields
            .Select((field, index) => FieldMatcher.Match(field, clues[index], screenCandidates, noTakenValue, options, context))
            .ToArray();
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
                : FieldMatcher.Match(fields[index], clues[index], screenCandidates, takenValues, options, context);
            if (match is { IsConfident: true, Candidate: not null })
            {
                _ = takenValues.Add(match.Candidate.Value);

                // The label it was read under is kept too: the same value under another label may still go elsewhere
                _ = takenValues.Add(FieldMatcher.TakenUnder(match.Candidate));
            }

            matches[index] = match;
        }

        if (profileCandidates.Count > 0)
        {
            ProfileArbiter.Apply(fields, clues, matches, profileCandidates, takenValues, options, page);
        }

        if (historyCandidates.Count > 0)
        {
            FillGapsFromHistory(fields, clues, matches, historyCandidates, takenValues, options);
        }

        SplitAddresses(matches, clues, formFields.Concat(fields).Select(FieldClues.Of).ToList());
        CheckPostalCodeAndCommune(matches, clues, data);
        LeaveValuesReadFromImagesToTheUser(matches);
        return matches;
    }

    /// <summary>
    /// Safety net: a value read from the image of a window (OCR) may hold a misread character, such as a 8 read as a 3:
    /// it is proposed, never written without the user.
    /// </summary>
    /// <param name="matches">The matches of the form, changed in place.</param>
    private static void LeaveValuesReadFromImagesToTheUser(FieldMatch[] matches)
    {
        for (var index = 0; index < matches.Length; index++)
        {
            if (matches[index] is { IsConfident: true, Candidate.IsReadFromImage: true } match)
            {
                matches[index] = match with { IsConfident = false, Reason = MatchReasons.ReadFromImage(match.Candidate) };
            }
        }
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
                Reason = screenMatch.Suggestions.Count == 0 && historyMatch.Suggestions.Count > 0 ? historyMatch.Reason : screenMatch.Reason,
            };
        }
    }

    private static bool AsksFirstNameOnly(FieldClues clues)
    {
        return clues.ExpectedKinds.Contains(CandidateKind.FirstName);
    }

    // « 9 allée des Tilleuls, Bâtiment B, 51430 Tinqueux » in « Adresse », next to « Code postal » and « Commune »: the
    // address keeps the street, the two fields take the postal code and the commune of that same address
    private static void SplitAddresses(FieldMatch[] matches, FieldClues[] clues, List<FieldClues> formClues)
    {
        var formAsksPostalCode = formClues.Exists(clue => clue.Mentions(LabelConcepts.PostalCode));
        var formAsksCommune = formClues.Exists(clue => clue.ExpectedKinds.Contains(CandidateKind.City) && !clue.Mentions(LabelConcepts.Address));
        if (!formAsksPostalCode || !formAsksCommune)
        {
            return;
        }

        for (var index = 0; index < matches.Length; index++)
        {
            if (!clues[index].Mentions(LabelConcepts.Address) || clues[index].Mentions(LabelConcepts.PostalCode)
                || matches[index] is not { IsConfident: true, Candidate: { } address }
                || AddressEndRegex().Match(address.Value) is not { Success: true } end)
            {
                continue;
            }

            matches[index] = matches[index] with { Candidate = address with { Value = end.Groups["street"].Value.Trim() } };
            FillFromAddress(matches, clues, address, end.Groups["postalCode"].Value, CandidateKind.Number, clue => clue.Mentions(LabelConcepts.PostalCode));
            FillFromAddress(matches, clues, address, end.Groups["commune"].Value.Trim(), CandidateKind.City, clue => clue.ExpectedKinds.Contains(CandidateKind.City) && !clue.Mentions(LabelConcepts.Address));
        }
    }

    // A field still unsure takes the value of the address chosen for the form; a sure one is left as it is
    private static void FillFromAddress(FieldMatch[] matches, FieldClues[] clues, Candidate address, string value, CandidateKind kind, Func<FieldClues, bool> isTarget)
    {
        for (var index = 0; index < matches.Length; index++)
        {
            if (isTarget(clues[index]) && !matches[index].IsConfident)
            {
                matches[index] = matches[index] with
                {
                    Candidate = address with { Value = value, Kind = kind },
                    IsConfident = true,
                    Score = Math.Max(matches[index].Score, FieldMatcher.ConfidenceThreshold),
                    Reason = string.Empty,
                };
            }
        }
    }

    [GeneratedRegex(@"^(?<street>.+?),\s*(?<postalCode>\d{5})\s+(?<commune>[^,]+)$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AddressEndRegex();

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
            var reason = MatchReasons.PostalCodeAndCommune(postalCode.Value, commune.Value);
            matches[postalCodeIndex] = matches[postalCodeIndex] with { IsConfident = false, Reason = reason };
            matches[communeIndex] = matches[communeIndex] with { IsConfident = false, Reason = reason };
        }
    }
}
