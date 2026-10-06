namespace PasteFlow.Core.Forms;

/// <summary>
/// Shows the user which fields of the form still need their attention, with the values that could fit them.
/// </summary>
public interface IFieldHighlighter
{
    /// <summary>
    /// Highlights the given fields, replacing the previous highlights; an empty list removes them.
    /// </summary>
    /// <param name="fields">The fields to highlight, as returned by a recent reading of the form, with their suggestions.</param>
    void Highlight(IReadOnlyList<FieldToCheck> fields);
}
