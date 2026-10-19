namespace PasteFlow.Core.Hints;

/// <summary>
/// Makes the codes of letters put on the values seen on screen, as Vimium does in a browser: typing a code pastes its
/// value.
/// </summary>
/// <remarks>
/// <para>The letters are those of the middle row that are at the same place on a French (AZERTY) and an English
/// (QWERTY) keyboard, the ones under the fingers at rest: F, J, D, K, S, L, G, H.</para>
/// <para>All the codes have the same length, so that no code is the start of another: typing a whole code always
/// means that value, never a longer one.</para>
/// </remarks>
public static class HintCodes
{
    /// <summary>
    /// The letters used, the easiest first.
    /// </summary>
    public const string Letters = "FJDKSLGH";

    /// <summary>
    /// Makes a code for each value.
    /// </summary>
    /// <param name="count">The number of values.</param>
    /// <returns>The codes, as many as the values, all of the same length: one letter for up to 8 values, two for up to
    /// 64, and so on.</returns>
    public static IReadOnlyList<string> Create(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var length = 1;
        var capacity = Letters.Length;
        while (capacity < count)
        {
            length++;
            capacity *= Letters.Length;
        }

        var codes = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            var code = new char[length];
            var rest = index;

            // The first letter changes last: values close in the list share their first letter, the easy ones come first
            for (var position = length - 1; position >= 0; position--)
            {
                code[position] = Letters[rest % Letters.Length];
                rest /= Letters.Length;
            }

            codes.Add(new string(code));
        }

        return codes;
    }

    /// <summary>
    /// Tells what the letters typed so far mean.
    /// </summary>
    /// <param name="codes">The codes shown.</param>
    /// <param name="typed">The letters typed, in any case.</param>
    /// <returns>The index of the code typed whole, or <see langword="null"/> if none is typed whole yet; and whether
    /// any code still starts with the letters typed.</returns>
    public static (int? Chosen, bool CanGoOn) Read(IReadOnlyList<string> codes, string typed)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(typed);

        var upper = typed.ToUpperInvariant();
        for (var index = 0; index < codes.Count; index++)
        {
            if (string.Equals(codes[index], upper, StringComparison.Ordinal))
            {
                return (index, true);
            }
        }

        return (null, codes.Any(code => code.StartsWith(upper, StringComparison.Ordinal)));
    }
}
