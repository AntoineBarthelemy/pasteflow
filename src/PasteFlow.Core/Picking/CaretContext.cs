namespace PasteFlow.Core.Picking;

/// <summary>
/// Where the user's text cursor is when the shortcut is pressed.
/// </summary>
public sealed record CaretContext
{
    /// <summary>
    /// Gets a value indicating whether the cursor is in a place where text can be typed.
    /// </summary>
    public required bool IsEditable { get; init; }

    /// <summary>
    /// Gets the identifier of the form field holding the cursor, as given by the last reading of the form, or
    /// <see langword="null"/> when the cursor is in a document rather than a field.
    /// </summary>
    public string? FocusedFieldId { get; init; }

    /// <summary>
    /// Gets the label of the place holding the cursor, such as "Message", or an empty string.
    /// </summary>
    public string FieldLabel { get; init; } = string.Empty;

    /// <summary>
    /// Gets the text written just before the cursor, such as "Tiens, mon mail : ", or an empty string when unknown.
    /// </summary>
    public string TextBefore { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the cursor is in a writing area filling most of its window, such as the page of
    /// Notepad or WordPad, or in the grid of a spreadsheet such as Excel: the small boxes around it (font, size, name
    /// box, formula bar, status bar) do not make it a form.
    /// </summary>
    public bool IsDocument { get; init; }
}
