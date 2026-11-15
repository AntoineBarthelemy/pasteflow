namespace PasteFlow.App.Screen;

/// <summary>
/// A text copied by the user.
/// </summary>
internal sealed record ClipboardCopy
{
    /// <summary>
    /// Gets the text copied.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the time of the copy, in UTC.
    /// </summary>
    public required DateTime CopiedAt { get; init; }
}
