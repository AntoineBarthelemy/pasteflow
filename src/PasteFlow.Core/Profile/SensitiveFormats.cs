using System.Globalization;
using System.Text;
using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Profile;

/// <summary>
/// Recognizes sensitive values by their format alone, whatever the field or the topic they come with:
/// an IBAN, a French social security number, a payment card number.
/// </summary>
/// <remarks>
/// Each format is checked with its key (IBAN modulo 97, social security key, Luhn for cards), so that an ordinary
/// number is not taken for a sensitive one by chance.
/// </remarks>
public static class SensitiveFormats
{
    private const int SocialSecurityNumberLength = 15;
    private const int SocialSecurityBodyLength = 13;
    private const int SocialSecurityModulus = 97;
    private const int MinIbanLength = 15;

    /// <summary>
    /// Determines whether a value has a sensitive format.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for an IBAN, a social security number or a payment card number; otherwise <see langword="false"/>.</returns>
    public static bool IsSensitiveValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return IsIban(value) || IsFrenchSocialSecurityNumber(value) || IsPaymentCardNumber(value);
    }

    /// <summary>
    /// Determines whether a value is an IBAN with a correct key.
    /// </summary>
    /// <param name="value">The value, with or without spaces.</param>
    /// <returns><see langword="true"/> for a valid IBAN; otherwise <see langword="false"/>.</returns>
    public static bool IsIban(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var compact = Compact(value);
        return compact.Length >= MinIbanLength
            && char.IsAsciiLetter(compact[0]) && char.IsAsciiLetter(compact[1])
            && char.IsAsciiDigit(compact[2]) && char.IsAsciiDigit(compact[3])
            && ValueValidator.IsValid(CandidateKind.Iban, compact);
    }

    /// <summary>
    /// Determines whether a value is a French social security number (NIR) with a correct key.
    /// </summary>
    /// <param name="value">The value, with or without spaces.</param>
    /// <returns><see langword="true"/> for a valid social security number; otherwise <see langword="false"/>.</returns>
    public static bool IsFrenchSocialSecurityNumber(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var compact = Compact(value).ToUpperInvariant();
        if (compact.Length != SocialSecurityNumberLength || compact[0] is not ('1' or '2' or '3' or '4' or '7' or '8'))
        {
            return false;
        }

        // Corsica: the departments 2A and 2B are counted as 19 and 18, then a fixed amount is taken off
        var body = compact[..SocialSecurityBodyLength];
        var correction = 0L;
        if (body[5..7] == "2A")
        {
            body = body[..5] + "19" + body[7..];
            correction = 1_000_000;
        }
        else if (body[5..7] == "2B")
        {
            body = body[..5] + "18" + body[7..];
            correction = 2_000_000;
        }

        var key = compact[SocialSecurityBodyLength..];
        if (!body.All(char.IsAsciiDigit) || !key.All(char.IsAsciiDigit))
        {
            return false;
        }

        var number = long.Parse(body, CultureInfo.InvariantCulture) - correction;
        var expectedKey = SocialSecurityModulus - (number % SocialSecurityModulus);
        return expectedKey == int.Parse(key, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Determines whether a value is a payment card number: 15 or 16 digits with a correct Luhn key.
    /// </summary>
    /// <param name="value">The value, with or without spaces.</param>
    /// <returns><see langword="true"/> for a payment card number; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// Other lengths are left out on purpose: a SIRET number has 14 digits and also follows the Luhn rule.
    /// A social security number has 15 digits and its own key, so it is never taken for a card.
    /// </remarks>
    public static bool IsPaymentCardNumber(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var compact = Compact(value);
        if (compact.Length is not (15 or 16) || !compact.All(char.IsAsciiDigit) || IsFrenchSocialSecurityNumber(compact))
        {
            return false;
        }

        var sum = 0;
        for (var index = 0; index < compact.Length; index++)
        {
            var digit = compact[compact.Length - 1 - index] - '0';
            if (index % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// Removes the spaces, dots and dashes that group the characters of a number.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value without separators.</returns>
    internal static string Compact(string value)
    {
        var compact = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character) && character is not ('.' or '-'))
            {
                _ = compact.Append(character);
            }
        }

        return compact.ToString();
    }
}
