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

    /// <summary>
    /// Gets a value indicating whether the text was read from the image of the window (OCR), because the window gives
    /// no text: it may hold misread characters.
    /// </summary>
    public bool IsReadFromImage { get; init; }

    /// <summary>
    /// Gets the filled fields of the window, each with its label; empty for the form being filled.
    /// </summary>
    public IReadOnlyList<FieldText> Fields { get; init; } = Array.Empty<FieldText>();

    /// <summary>
    /// Gets the address shown in the address bar when the window is a browser, such as "github.com/login"; empty for
    /// other windows. Only read for the active window.
    /// </summary>
    public string Address { get; init; } = string.Empty;
}
