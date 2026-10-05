namespace PasteFlow.Core.Screen;

/// <summary>
/// The text displayed in a window of the desktop.
/// </summary>
public sealed record WindowText
{
    /// <summary>
    /// Gets the title of the window.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the text read in the window, lines separated by line breaks.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the position of the window in the Alt+Tab order: 0 for the active window, 1 for the one just behind, and so on.
    /// </summary>
    public required int Rank { get; init; }
}
