using PasteFlow.Core.Candidates;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Hints;

/// <summary>
/// Chooses, among the texts seen on screen, the values that get a code of letters.
/// </summary>
/// <remarks>
/// <para>The recognized values come first: emails, phone numbers, dates, amounts, references, names... A short line
/// that is not a label ("Email :") gets a code too, while there is room: a name of company or a title, for example.</para>
/// <para>A value seen twice gets a single code, on its highest place. At most <see cref="MaxTargets"/> values get a
/// code, so that two letters are always enough.</para>
/// </remarks>
public static class HintTargets
{
    /// <summary>
    /// The maximum number of values that get a code.
    /// </summary>
    public const int MaxTargets = 64;

    /// <summary>
    /// The maximum length of a plain line that gets a code; a longer one is a sentence, not a value to paste.
    /// </summary>
    public const int MaxPlainLength = 60;

    private const int MaxTextLength = 400;

    /// <summary>
    /// Chooses the values that get a code, with the official lists of the application.
    /// </summary>
    /// <param name="texts">The texts seen on screen.</param>
    /// <returns>The values with their codes, from the top of the screen to the bottom.</returns>
    public static IReadOnlyList<HintTarget> From(IEnumerable<ScreenText> texts)
    {
        return From(texts, ReferenceData.Shared);
    }

    /// <summary>
    /// Chooses the values that get a code.
    /// </summary>
    /// <param name="texts">The texts seen on screen.</param>
    /// <param name="data">The official lists used to recognize names, communes and companies.</param>
    /// <returns>The values with their codes, from the top of the screen to the bottom.</returns>
    public static IReadOnlyList<HintTarget> From(IEnumerable<ScreenText> texts, ReferenceData data)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(data);

        var recognized = new List<(string Value, CandidateKind Kind, ScreenText Place)>();
        var plain = new List<(string Value, CandidateKind Kind, ScreenText Place)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var place in texts.OrderBy(text => text.Top).ThenBy(text => text.Left))
        {
            var text = place.Text.Trim();
            if (text.Length == 0 || text.Length > MaxTextLength)
            {
                continue;
            }

            foreach (var piece in TextSegmenter.Segment(text, data))
            {
                var value = piece.Value.Trim();
                if (piece.Kind != CandidateKind.Text)
                {
                    if (seen.Add(value))
                    {
                        recognized.Add((value, piece.Kind, place));
                    }
                }
                else if (IsPlainValue(value) && seen.Add(value))
                {
                    plain.Add((value, piece.Kind, place));
                }
            }
        }

        var chosen = recognized
            .Concat(plain)
            .Take(MaxTargets)
            .OrderBy(target => target.Place.Top)
            .ThenBy(target => target.Place.Left)
            .ToList();
        var codes = HintCodes.Create(chosen.Count);
        return chosen
            .Select((target, index) => new HintTarget { Value = target.Value, Kind = target.Kind, Code = codes[index], Place = target.Place })
            .ToList();
    }

    private static bool IsPlainValue(string value)
    {
        // A label such as "Email :" announces a value, it is not one; a single character is never worth a code
        return value.Length is >= 2 and <= MaxPlainLength
            && !value.EndsWith(':')
            && value.Any(char.IsLetterOrDigit);
    }
}
