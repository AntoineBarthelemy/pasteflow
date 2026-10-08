using System.Text.RegularExpressions;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.History;

/// <summary>
/// Recognizes the fields and values the history must never learn: payment cards, security and one-time codes,
/// social security numbers, bank details, passwords not marked as such.
/// </summary>
/// <remarks>
/// Many of these fields are plain text fields, not password fields, so their label and technical name are checked.
/// </remarks>
public static partial class SensitiveFields
{
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    /// Determines whether a value written into a field must never be learned.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The value written.</param>
    /// <returns><see langword="true"/> if the field or the value is sensitive; otherwise <see langword="false"/>.</returns>
    public static bool IsSensitive(FormField field, string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);

        if (field.Kind == FieldKind.Password)
        {
            return true;
        }

        var description = ReferenceText.Normalize($"{field.Label} {SplitWords(field.TechnicalName)} {field.Hint}");
        try
        {
            return SensitiveWordsRegex().IsMatch(description) || CardNumberRegex().IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    private static string SplitWords(string technicalName)
    {
        // "ccNumber", "cc-csc", "one_time_code" become separate words
        return CamelCaseRegex().Replace(technicalName, " ").Replace('_', ' ').Replace('-', ' ');
    }

    [GeneratedRegex(@"\b(?:CARTE|CB|CVV|CVC|CSC|CRYPTOGRAMME|CC|CARD|PIN|OTP|SMS|IBAN|BIC|RIB|NIR|SSN|PASSWORD|PASSE|MDP|SECURITE|SECURITY|VERIFICATION|CODE ONE TIME|ONE TIME|TOTP|2FA|EXPIRATION|EXPIRY)\b", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex SensitiveWordsRegex();

    // 13 to 19 digits, possibly grouped: the length of payment card numbers
    [GeneratedRegex(@"^\s*(?:\d[ -]?){12,18}\d\s*$", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex CardNumberRegex();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex CamelCaseRegex();
}
