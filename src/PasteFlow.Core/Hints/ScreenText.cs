namespace PasteFlow.Core.Hints;

/// <summary>
/// A text seen on screen, with where it is.
/// </summary>
public sealed record ScreenText
{
    /// <summary>
    /// Gets the text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the left edge of the text, in screen pixels.
    /// </summary>
    public double Left { get; init; }

    /// <summary>
    /// Gets the top edge of the text, in screen pixels.
    /// </summary>
    public double Top { get; init; }

    /// <summary>
    /// Gets the width of the text, in screen pixels.
    /// </summary>
    public double Width { get; init; }

    /// <summary>
    /// Gets the height of the text, in screen pixels.
    /// </summary>
    public double Height { get; init; }
}
