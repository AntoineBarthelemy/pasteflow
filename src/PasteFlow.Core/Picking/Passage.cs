namespace PasteFlow.Core.Picking;

/// <summary>
/// A piece of text found in a window, ready to be pasted.
/// </summary>
public sealed record Passage
{
    /// <summary>
    /// Gets the text to paste.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the title of the window it comes from.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Gets the closeness of that window: 0 for the active one, higher for the windows behind.
    /// </summary>
    public required int SourceRank { get; init; }

    /// <summary>
    /// Gets the line the passage was found in, to widen or shrink it.
    /// </summary>
    internal string Block { get; init; } = string.Empty;

    /// <summary>
    /// Gets where the passage starts in its line.
    /// </summary>
    internal int Start { get; init; }

    /// <summary>
    /// Gets where the passage ends in its line, excluded.
    /// </summary>
    internal int End { get; init; }

    /// <summary>
    /// Makes the same passage with other bounds in its line.
    /// </summary>
    /// <param name="start">The new start.</param>
    /// <param name="end">The new end, excluded.</param>
    /// <returns>The passage.</returns>
    internal Passage WithBounds(int start, int end)
    {
        return this with { Start = start, End = end, Text = Block[start..end].Trim() };
    }
}
