namespace PasteFlow.Core.Forms;

/// <summary>
/// Shows the user which fields of the form still need their attention.
/// </summary>
public interface IFieldHighlighter
{
    /// <summary>
    /// Highlights the given fields, replacing the previous highlights; an empty list removes them.
    /// </summary>
    /// <param name="fields">The fields to highlight, as returned by a recent reading of the form.</param>
    void Highlight(IReadOnlyList<FormField> fields);
}
