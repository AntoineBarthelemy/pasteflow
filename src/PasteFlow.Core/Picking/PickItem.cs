namespace PasteFlow.Core.Picking;

/// <summary>
/// One choice of the list shown at the cursor.
/// </summary>
public sealed record PickItem
{
    /// <summary>
    /// Gets the text pasted when this choice is made.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the title of the window the text comes from.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Gets what the text is, such as "e-mail", or an empty string for a passage of text.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the passage behind the text, which can be widened or shrunk, or <see langword="null"/> for a value.
    /// </summary>
    public Passage? Passage { get; init; }
}
