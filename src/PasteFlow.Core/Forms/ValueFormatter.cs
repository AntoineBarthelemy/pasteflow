using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Forms;

/// <summary>
/// Writes a value in the format a field asks for, as shown by its label or its help text: "JJ/MM/AAAA",
/// "ex : 0612345678", "AAAA-MM-JJ"...
/// </summary>
/// <remarks>
/// Only the shape changes, never the value: a date stays the same day, a phone number the same line. Without a
/// format shown, or for a value that cannot be read, the value is written as found.
/// </remarks>
public static partial class ValueFormatter
{
    private const int RegexTimeoutMilliseconds = 1000;
    private const int FrenchPhoneDigits = 10;
    private const int IbanGroupSize = 4;

    private static readonly string[] DayFirstFormats = { "d/M/yyyy", "d/M/yy", "d.M.yyyy", "d.M.yy", "d-M-yyyy", "d-M-yy", "yyyy-M-d" };
    private static readonly string[] MonthFirstFormats = { "M/d/yyyy", "M/d/yy" };

    /// <summary>
    /// Adapts a value to the format its field asks for.
    /// </summary>
    /// <param name="value">The value found.</param>
    /// <param name="kind">The kind of the value.</param>
    /// <param name="field">The field to fill.</param>
    /// <returns>The value in the format of the field, or unchanged when the field shows no format.</returns>
    public static string Adapt(string value, CandidateKind kind, FormField field)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(field);

        var format = $"{field.Label} {field.Hint}";
        try
        {
            return kind switch
            {
                CandidateKind.Date => AdaptDate(value, format) ?? value,
                CandidateKind.Phone => AdaptPhone(value, format) ?? value,
                CandidateKind.Iban => AdaptIban(value, format) ?? value,
                CandidateKind.Amount => AdaptAmount(value, format) ?? value,
                _ => value,
            };
        }
        catch (RegexMatchTimeoutException)
        {
            return value;
        }
    }

    private static string? AdaptDate(string value, string format)
    {
        var target = DateFormatOf(format);
        if (target is null)
        {
            return null;
        }

        var text = value.Trim();
        return DateOnly.TryParseExact(text, DayFirstFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || DateOnly.TryParseExact(text, MonthFirstFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            ? date.ToString(target, CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// Finds the date format a field asks for, written as a pattern ("JJ/MM/AAAA", "YYYY-MM-DD") or shown by an example
    /// ("ex : 31/12/1990").
    /// </summary>
    /// <param name="format">The label and help text of the field.</param>
    /// <returns>The .NET format, or <see langword="null"/> when none is shown.</returns>
    private static string? DateFormatOf(string format)
    {
        var pattern = DatePatternRegex().Match(format);
        if (pattern.Success)
        {
            // "JJ/MM/AAAA" and "DD/MM/YYYY" become "dd/MM/yyyy"
            return pattern.Value.ToUpperInvariant().Replace('J', 'd').Replace('D', 'd').Replace('A', 'y').Replace('Y', 'y');
        }

        var example = DateExampleRegex().Match(format);
        if (!example.Success)
        {
            return null;
        }

        if (example.Groups["iso"].Success)
        {
            return "yyyy-MM-dd";
        }

        var separator = example.Groups["separator"].Value;
        var year = example.Groups["year"].Value.Length == 2 ? "yy" : "yyyy";
        return $"dd{separator}MM{separator}{year}";
    }

    private static string? AdaptPhone(string value, string format)
    {
        var example = PhoneExampleRegex().Match(format);
        var isCompact = (example.Success && !example.Value.Any(char.IsWhiteSpace))
            || format.Contains("sans espace", StringComparison.OrdinalIgnoreCase);
        var isInternational = example.Success && example.Value.StartsWith('+');
        if (!example.Success && !isCompact)
        {
            return null;
        }

        var national = FrenchNationalDigits(value);
        if (national is null)
        {
            return null;
        }

        var digits = isInternational ? "+33" + national[1..] : national;
        if (isCompact)
        {
            return digits;
        }

        // "06 12 34 56 78" or "+33 6 12 34 56 78"
        var groups = new List<string>();
        var rest = isInternational ? national[1..] : national;
        if (isInternational)
        {
            groups.Add("+33");
            groups.Add(rest[..1]);
            rest = rest[1..];
        }

        for (var index = 0; index < rest.Length; index += 2)
        {
            groups.Add(rest.Substring(index, Math.Min(2, rest.Length - index)));
        }

        return string.Join(' ', groups);
    }

    /// <summary>
    /// Gets the ten digits of a French phone number, starting with 0, whatever the way it is written.
    /// </summary>
    /// <param name="value">The phone number, such as "+33 (0)6 12 34 56 78" or "06.12.34.56.78".</param>
    /// <returns>The ten digits, or <see langword="null"/> for a number that is not French.</returns>
    private static string? FrenchNationalDigits(string value)
    {
        var text = value.Replace("(0)", string.Empty, StringComparison.Ordinal).Trim();
        var digits = string.Concat(text.Where(char.IsAsciiDigit));
        if (text.StartsWith("+33", StringComparison.Ordinal) && digits.Length == FrenchPhoneDigits + 1)
        {
            return "0" + digits[2..];
        }

        return !text.StartsWith('+') && digits.Length == FrenchPhoneDigits && digits[0] == '0' ? digits : null;
    }

    private static string? AdaptIban(string value, string format)
    {
        var example = IbanExampleRegex().Match(format);
        var isCompact = (example.Success && !example.Value.Contains(' ', StringComparison.Ordinal))
            || format.Contains("sans espace", StringComparison.OrdinalIgnoreCase);
        if (!example.Success && !isCompact)
        {
            return null;
        }

        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (isCompact)
        {
            return compact;
        }

        var builder = new StringBuilder(compact.Length + (compact.Length / IbanGroupSize));
        for (var index = 0; index < compact.Length; index++)
        {
            if (index > 0 && index % IbanGroupSize == 0)
            {
                _ = builder.Append(' ');
            }

            _ = builder.Append(compact[index]);
        }

        return builder.ToString();
    }

    private static string? AdaptAmount(string value, string format)
    {
        // A field showing a bare number ("0,00", "ex : 1234.50") wants the number only, without currency or spaces
        var example = NumberExampleRegex().Match(format);
        if (!example.Success)
        {
            return null;
        }

        var decimalSeparator = example.Groups["separator"].Value;
        var amount = AmountRegex().Match(value);
        if (!amount.Success)
        {
            return null;
        }

        var integerPart = string.Concat(amount.Groups["integer"].Value.Where(char.IsAsciiDigit));
        var cents = amount.Groups["cents"].Value;
        return cents.Length > 0 && decimalSeparator.Length > 0 ? $"{integerPart}{decimalSeparator}{cents}" : integerPart;
    }

    [GeneratedRegex(@"\b(?:(?:JJ|DD)[/.\-]?MM[/.\-]?(?:AAAA|YYYY|AA|YY)|MM/(?:JJ|DD)/(?:AAAA|YYYY)|(?:AAAA|YYYY)-MM-(?:JJ|DD))\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
    private static partial Regex DatePatternRegex();

    [GeneratedRegex(@"(?<!\d)(?:(?<iso>\d{4}-\d{2}-\d{2})|\d{1,2}(?<separator>[/.\-])\d{1,2}\k<separator>(?<year>\d{4}|\d{2}))(?!\d)", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex DateExampleRegex();

    [GeneratedRegex(@"(?:\+33[ .]?(?:\(0\)[ .]?)?[1-9]|0[1-9])(?:[ .]?\d{2}){4}(?!\d)", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex PhoneExampleRegex();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){2,7}(?: ?[A-Z0-9]{1,3})?\b", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex IbanExampleRegex();

    [GeneratedRegex(@"(?<![\d.,])\d+(?<separator>[.,])\d{2}(?![\d.,])", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex NumberExampleRegex();

    [GeneratedRegex(@"(?<integer>\d+(?:[   .]\d{3})*)(?:[.,](?<cents>\d{1,2}))?", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex AmountRegex();
}
