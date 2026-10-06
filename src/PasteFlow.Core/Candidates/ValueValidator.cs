using System.Globalization;

namespace PasteFlow.Core.Candidates;

/// <summary>
/// Checks that a value which has the shape of a known format is really valid: an IBAN with a correct check key,
/// a date that exists, a well-formed email address, a French postal code with a real department.
/// </summary>
public static class ValueValidator
{
    private const int IbanModulus = 97;
    private const int MinIbanLength = 15;
    private const int MaxIbanLength = 34;
    private const int PostalCodeLength = 5;

    private static readonly string[] DateFormats =
    {
        "d/M/yyyy", "d/M/yy", "d.M.yyyy", "d.M.yy", "d-M-yyyy", "d-M-yy", "yyyy-M-d",
        "M/d/yyyy", "M/d/yy",
    };

    /// <summary>
    /// Determines whether a value recognized as a given kind is valid for that kind.
    /// </summary>
    /// <param name="kind">The kind recognized from the shape of the value.</param>
    /// <param name="value">The value to check.</param>
    /// <returns>
    /// <see langword="true"/> if the value is valid, or if its kind has no particular rule; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsValid(CandidateKind kind, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return kind switch
        {
            CandidateKind.Iban => IsValidIban(value),
            CandidateKind.Date => IsValidDate(value),
            CandidateKind.Email => IsValidEmail(value),
            _ => true,
        };
    }

    /// <summary>
    /// Determines whether a value is a French postal code: five digits starting with a real department.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> for a valid French postal code; otherwise <see langword="false"/>.</returns>
    public static bool IsFrenchPostalCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var code = value.Trim();
        if (code.Length != PostalCodeLength || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        // Departments 01 to 95, overseas departments 97 and Monaco or overseas collectivities 98
        var department = int.Parse(code[..2], CultureInfo.InvariantCulture);
        return department is >= 1 and <= 95 or 97 or 98;
    }

    /// <summary>
    /// Checks the key of an IBAN: the first four characters are moved to the end, letters become numbers
    /// (A = 10, B = 11...) and the result divided by 97 must leave 1.
    /// </summary>
    /// <param name="value">The IBAN, with or without spaces.</param>
    /// <returns><see langword="true"/> if the key is correct; otherwise <see langword="false"/>.</returns>
    private static bool IsValidIban(string value)
    {
        var iban = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (iban.Length is < MinIbanLength or > MaxIbanLength || !iban.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var character in rearranged)
        {
            var number = char.IsAsciiDigit(character) ? character - '0' : character - 'A' + 10;
            remainder = ((remainder * (number < 10 ? 10 : 100)) + number) % IbanModulus;
        }

        return remainder == 1;
    }

    private static bool IsValidDate(string value)
    {
        return DateOnly.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    private static bool IsValidEmail(string value)
    {
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != value.LastIndexOf('@'))
        {
            return false;
        }

        var name = value[..at];
        var domain = value[(at + 1)..];
        return !name.StartsWith('.')
            && !name.EndsWith('.')
            && !value.Contains("..", StringComparison.Ordinal)
            && domain.Split('.').All(part => part.Length > 0 && !part.StartsWith('-') && !part.EndsWith('-'));
    }
}
