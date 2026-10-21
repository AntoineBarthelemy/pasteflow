using System.Globalization;
using System.Text;

namespace PasteFlow.Core.Reference;

/// <summary>
/// Puts names in a single comparable form, the same for the reference lists and for the text read on screen.
/// </summary>
public static class ReferenceText
{
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.Ordinal)
    {
        ["SAINT"] = "ST",
        ["SAINTE"] = "STE",
    };

    /// <summary>
    /// Normalizes a name: upper case, no accent, words separated by single spaces, "Saint" written "ST" as La Poste does.
    /// </summary>
    /// <param name="text">The name, as written anywhere.</param>
    /// <returns>The comparable form, such as "ST JEAN DE MONTS" for "Saint-Jean-de-Monts".</returns>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        foreach (var character in RemoveAccents(text))
        {
            _ = builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : ' ');
        }

        var words = builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => Abbreviations.GetValueOrDefault(word, word));
        return string.Join(' ', words);
    }

    /// <summary>
    /// Removes the accents of a text: "Châlons" becomes "Chalons".
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without accents, other characters unchanged.</returns>
    public static string RemoveAccents(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        foreach (var character in NormalizeUnicode(text, NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                _ = builder.Append(character);
            }
        }

        return NormalizeUnicode(builder.ToString(), NormalizationForm.FormC);
    }

    /// <summary>
    /// Normalizes the Unicode form of a text, even a text read on screen that holds half of an emoji, which the
    /// normalization of .NET refuses.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="form">The form wanted.</param>
    /// <returns>The normalized text; a half character alone is replaced by "\uFFFD".</returns>
    public static string NormalizeUnicode(string text, NormalizationForm form)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            return text.Normalize(form);
        }
        catch (ArgumentException)
        {
            return RepairHalves(text).Normalize(form);
        }
    }

    private static string RepairHalves(string text)
    {
        // An emoji is stored as two halves: one without the other is not a character
        var repaired = text.ToCharArray();
        for (var index = 0; index < repaired.Length; index++)
        {
            if (char.IsHighSurrogate(repaired[index]) && index + 1 < repaired.Length && char.IsLowSurrogate(repaired[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(repaired[index]))
            {
                repaired[index] = '\uFFFD';
            }
        }

        return new string(repaired);
    }
}
