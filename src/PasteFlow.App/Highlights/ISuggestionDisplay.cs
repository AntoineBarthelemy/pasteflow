using System.Windows;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Highlights;

/// <summary>
/// Shows the values proposed for the field the user is in.
/// </summary>
internal interface ISuggestionDisplay
{
    /// <summary>
    /// Occurs when the user picked a value and it was written into the field.
    /// </summary>
    event EventHandler? ValuePicked;

    /// <summary>
    /// Shows the values proposed for a field, below it; hides them if the field has none.
    /// </summary>
    /// <param name="fieldToCheck">The field the user is in, with its suggestions.</param>
    /// <param name="fieldBounds">The position of the field in screen pixels.</param>
    void Show(FieldToCheck fieldToCheck, Rect fieldBounds);

    /// <summary>
    /// Hides the values proposed, if shown.
    /// </summary>
    void Hide();
}
