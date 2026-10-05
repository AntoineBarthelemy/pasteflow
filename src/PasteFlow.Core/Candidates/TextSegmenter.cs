namespace PasteFlow.Core.Candidates;

/// <summary>
/// Cuts a text into pieces that could be pasted.
/// </summary>
/// <remarks>
/// For now a piece is a line. Finer units of meaning (emails, numbers, dates, "label: value") come next.
/// </remarks>
public static class TextSegmenter
{
    /// <summary>
    /// The maximum length of a piece; longer lines are paragraphs, not values to paste.
    /// </summary>
    public const int MaxPieceLength = 200;

    private static readonly char[] LineBreaks = { '\r', '\n' };

    /// <summary>
    /// Cuts a text into distinct, trimmed and non-empty lines, in their order of appearance.
    /// </summary>
    /// <param name="text">The text to cut.</param>
    /// <returns>The pieces of the text.</returns>
    public static IReadOnlyList<string> Segment(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text
            .Split(LineBreaks, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Length <= MaxPieceLength)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
