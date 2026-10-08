using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PasteFlow.Core.Candidates;

/// <summary>
/// Recognizes example values shown to guide the user, such as "ex : jean.dupont@exemple.fr" or "06 00 00 00 00",
/// which must never be pasted as if they were real data.
/// </summary>
public static partial class SampleValues
{
    private const int RegexTimeoutMilliseconds = 1000;
    private const int PhoneSubscriberDigits = 8;

    private static readonly HashSet<string> SampleEmailDomains = new(StringComparer.Ordinal)
    {
        "example.com", "example.org", "example.net", "example.fr", "exemple.fr", "exemple.com", "exemple.org",
        "domain.com", "domaine.fr", "domaine.com", "email.com", "test.com", "test.fr", "mail.test",
    };

    private static readonly HashSet<string> SampleEmailNames = new(StringComparer.Ordinal)
    {
        "jean.dupont", "jeandupont", "john.doe", "johndoe", "jane.doe", "janedoe", "prenom.nom", "nom.prenom",
        "firstname.lastname", "first.last", "nom", "prenom", "name", "user", "utilisateur", "test", "exemple", "example",
    };

    private static readonly HashSet<string> SampleTexts = new(StringComparer.Ordinal)
    {
        "jean dupont", "john doe", "jane doe", "nom prenom", "prenom nom", "nom", "prenom", "first name", "last name",
        "jj/mm/aaaa", "jj-mm-aaaa", "dd/mm/yyyy", "mm/dd/yyyy", "aaaa-mm-jj", "yyyy-mm-dd", "hh:mm",
    };

    private static readonly string[] SampleWords =
    {
        "exemple", "example", "sample", "lorem", "ipsum", "votre", "your", "xxx",
    };

    /// <summary>
    /// Determines whether a candidate is an example value rather than real data.
    /// </summary>
    /// <param name="candidate">The candidate to check.</param>
    /// <returns><see langword="true"/> if the candidate is an example; otherwise <see langword="false"/>.</returns>
    public static bool IsSample(Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (SampleLabelRegex().IsMatch(Normalize(candidate.Label)))
        {
            return true;
        }

        var value = Normalize(candidate.Value);
        if (SampleWords.Any(word => value.Contains(word, StringComparison.Ordinal)) || SampleTexts.Contains(value))
        {
            return true;
        }

        return candidate.Kind switch
        {
            CandidateKind.Email => IsSampleEmail(value),
            CandidateKind.Phone => IsSamplePhone(value),
            _ => RepeatedCharacterRegex().IsMatch(value),
        };
    }

    /// <summary>
    /// Determines whether a value appears in the help texts of the form, such as a placeholder.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="hints">The help texts of the fields of the form.</param>
    /// <returns><see langword="true"/> if one of the help texts contains the value; otherwise <see langword="false"/>.</returns>
    public static bool AppearsInHints(string value, IEnumerable<string> hints)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(hints);

        var trimmed = value.Trim();
        return trimmed.Length > 0
            && hints.Any(hint => hint.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSampleEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at > 0
            && (SampleEmailNames.Contains(email[..at]) || SampleEmailDomains.Contains(email[(at + 1)..]));
    }

    /// <summary>
    /// Recognizes phone numbers made of a repeated digit or of a counting sequence, such as 06 00 00 00 00,
    /// 06 12 34 56 78 or 01 23 45 67 89.
    /// </summary>
    /// <param name="phone">The normalized phone number.</param>
    /// <returns><see langword="true"/> for a sample phone number; otherwise <see langword="false"/>.</returns>
    private static bool IsSamplePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < PhoneSubscriberDigits)
        {
            return false;
        }

        var subscriber = digits[^PhoneSubscriberDigits..];
        return subscriber.Distinct().Count() == 1
            || subscriber == "12345678"
            || subscriber == "23456789"
            || digits.EndsWith("0123456789", StringComparison.Ordinal);
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                _ = builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    [GeneratedRegex(@"\b(?:ex|exemples?|examples?|e\.?\s?g|format|sample|modele|placeholder)\b", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex SampleLabelRegex();

    [GeneratedRegex(@"^(?:x{3,}|0{3,}|a{3,}|9{3,})$", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex RepeatedCharacterRegex();
}
