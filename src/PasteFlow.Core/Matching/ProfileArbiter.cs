using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Matching;

/// <summary>
/// Decides, field by field, between the values of the screen and the values of the profile of the user.
/// </summary>
/// <remarks>
/// <para>Who the field is about decides (<see cref="FieldOwnership"/>):</para>
/// <list type="bullet">
/// <item><description>the user ("Votre e-mail", a page "Créer un compte"): the profile wins over the screen;</description></item>
/// <item><description>someone else ("E-mail du contact", a page "Nouveau client"): the profile is left aside;</description></item>
/// <item><description>a copy from a form behind: the screen wins;</description></item>
/// <item><description>nobody known: the profile fills the field only if the screen offers nothing; if both offer a
/// different value, the field is left to the user with both values proposed. No bet.</description></item>
/// </list>
/// <para>A sensitive value of the profile only goes into a field known to be the user's.</para>
/// <para>When the profile holds several values of a kind, such as two emails, the one the user writes on this site
/// is chosen, else the one written most often; if none stands out, both are only proposed.</para>
/// </remarks>
internal static class ProfileArbiter
{
    private static readonly string[] AccountSites = { "linkedin", "github" };

    /// <summary>
    /// Brings the values of the profile into the matches of the screen.
    /// </summary>
    /// <param name="fields">The fields of the form.</param>
    /// <param name="clues">The clues of each field.</param>
    /// <param name="matches">The matches found on screen, changed in place.</param>
    /// <param name="profileCandidates">The values of the profile.</param>
    /// <param name="takenValues">The values already given to fields, completed in place.</param>
    /// <param name="options">The thresholds of confidence.</param>
    /// <param name="page">What the window being filled tells.</param>
    public static void Apply(
        IReadOnlyList<FormField> fields,
        FieldClues[] clues,
        FieldMatch[] matches,
        IReadOnlyList<Candidate> profileCandidates,
        HashSet<string> takenValues,
        MatchingOptions options,
        PageContext page)
    {
        var noTakenValue = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < matches.Length; index++)
        {
            var field = fields[index];
            var screenMatch = matches[index];
            var owner = FieldOwnership.Of(field, page);

            // Someone else's field, or a copy from a form behind: the profile has nothing to say
            if (owner == FieldOwner.SomeoneElse || screenMatch is { IsConfident: true, Candidate.IsTypedInField: true })
            {
                continue;
            }

            // A sensitive value, such as an IBAN, only goes into a field known to be the user's: the IBAN of a payment form
            // is often the one of the beneficiary
            var eligible = EligibleFor(field, profileCandidates)
                .Where(candidate => !candidate.IsSensitive || owner == FieldOwner.User)
                .ToList();
            if (eligible.Count == 0)
            {
                continue;
            }

            // The value the screen gave to this very field stays available to the profile: it is not taken by another field
            var isConfirmation = clues[index].IsConfirmation;
            var taken = isConfirmation ? noTakenValue : new HashSet<string>(takenValues, StringComparer.Ordinal);
            if (screenMatch is { IsConfident: true, Candidate: { } screenValue })
            {
                _ = taken.Remove(screenValue.Value);
            }

            var profileMatch = FieldMatcher.Match(field, clues[index], Preferred(eligible, page.Site), taken, options);
            var profileSuggestions = profileMatch.Suggestions
                .Concat(FieldMatcher.Match(field, clues[index], eligible, taken, options).Suggestions)
                .DistinctBy(candidate => candidate.Value, StringComparer.Ordinal)
                .ToList();
            if (profileSuggestions.Count == 0 && profileMatch is not { IsConfident: true, Candidate: not null })
            {
                continue;
            }

            var result = Decide(owner, screenMatch, profileMatch, profileSuggestions);
            if (result is { IsConfident: true, Candidate.IsFromProfile: true } && !isConfirmation)
            {
                _ = takenValues.Add(result.Candidate.Value);
            }

            matches[index] = result;
        }
    }

    private static FieldMatch Decide(FieldOwner owner, FieldMatch screenMatch, FieldMatch profileMatch, List<Candidate> profileSuggestions)
    {
        var isProfileSure = profileMatch is { IsConfident: true, Candidate: not null };
        var screenOffers = screenMatch.Candidate is not null && screenMatch.Score >= FieldMatcher.SuggestionThreshold;
        var bothSuggestions = profileSuggestions
            .Concat(screenMatch.Suggestions)
            .DistinctBy(candidate => candidate.Value, StringComparer.Ordinal)
            .Take(FieldMatcher.MaxSuggestions)
            .ToList();

        if (isProfileSure && screenOffers && IsSameValue(screenMatch.Candidate!, profileMatch.Candidate!))
        {
            // Both agree: nothing to decide
            return screenMatch.IsConfident ? screenMatch : profileMatch with { Suggestions = bothSuggestions };
        }

        if (isProfileSure && (owner == FieldOwner.User || !screenOffers))
        {
            return profileMatch with { Suggestions = bothSuggestions };
        }

        if (isProfileSure || owner == FieldOwner.User || !screenMatch.IsConfident)
        {
            // Two different values and nobody known, or the user's own field with several values in the profile:
            // left to the user, with the values of both proposed
            return screenMatch with { IsConfident = false, Suggestions = bothSuggestions };
        }

        // The profile hesitates and the field is not known to be the user's: the screen keeps its sure value
        return screenMatch;
    }

    private static bool IsSameValue(Candidate first, Candidate second)
    {
        return string.Equals(
            SensitiveFormats.Compact(first.Value),
            SensitiveFormats.Compact(second.Value),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Keeps the values of the profile a field can take: a LinkedIn or GitHub address only goes into a field that
    /// names that site, and such a field only takes that site's address.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="profileCandidates">The values of the profile.</param>
    /// <returns>The values the field can take.</returns>
    private static List<Candidate> EligibleFor(FormField field, IReadOnlyList<Candidate> profileCandidates)
    {
        var fieldWords = ReferenceText.Normalize($"{field.Label} {field.Hint} {field.TechnicalName}");
        var namedSite = Array.Find(AccountSites, site => fieldWords.Contains(site.ToUpperInvariant(), StringComparison.Ordinal));
        return profileCandidates
            .Where(candidate =>
            {
                var accountSite = Array.Find(AccountSites, site => candidate.Value.Contains($"{site}.", StringComparison.OrdinalIgnoreCase));
                return candidate.Kind != CandidateKind.Url || string.Equals(accountSite, namedSite, StringComparison.Ordinal);
            })
            .ToList();
    }

    /// <summary>
    /// Keeps, among several values of the profile for the same topic, the one the user writes on this site, or else
    /// the one written most often; all of them if none stands out.
    /// </summary>
    /// <param name="candidates">The values of the profile a field can take.</param>
    /// <param name="site">The site of the page being filled, or an empty string.</param>
    /// <returns>The values preferred.</returns>
    private static List<Candidate> Preferred(List<Candidate> candidates, string site)
    {
        var preferred = new List<Candidate>();
        foreach (var topic in candidates.GroupBy(candidate => (candidate.Kind, candidate.Label)))
        {
            var values = topic.ToList();
            preferred.AddRange(StandsOut(values, candidate => SiteUses.CountFor(candidate.ProfileSites, site))
                ?? StandsOut(values, candidate => candidate.ProfileUseCount)
                ?? values);
        }

        return preferred;
    }

    private static List<Candidate>? StandsOut(List<Candidate> values, Func<Candidate, int> uses)
    {
        if (values.Count == 1)
        {
            return values;
        }

        var ranked = values.OrderByDescending(uses).ToList();
        var best = uses(ranked[0]);
        return best > 0 && best > uses(ranked[1]) ? new List<Candidate> { ranked[0] } : null;
    }
}
