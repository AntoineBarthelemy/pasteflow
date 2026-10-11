using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Picking;

/// <summary>
/// Decides when the shortcut pastes at the cursor rather than filling a form, and what the cursor asks for.
/// </summary>
/// <remarks>
/// A form is a window with several text fields: there, the shortcut fills the form as a whole. A text written alone,
/// such as the body of a message, a document or a chat box, is filled at the cursor: the words just before the
/// cursor tell what is expected, as a label would ("mon mail :" asks for an email).
/// </remarks>
public static class FreePaste
{
    /// <summary>
    /// The longest text before the cursor read as a label.
    /// </summary>
    public const int MaxLabelLength = 60;

    private const string CaretFieldId = "caret";

    /// <summary>
    /// Determines whether the cursor is in a text written alone rather than in a form.
    /// </summary>
    /// <param name="fields">The fields of the active window.</param>
    /// <param name="caret">Where the cursor is.</param>
    /// <returns><see langword="true"/> to paste at the cursor; <see langword="false"/> to fill the form.</returns>
    public static bool IsFreeText(IReadOnlyList<FormField> fields, CaretContext caret)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(caret);

        if (!caret.IsEditable)
        {
            return false;
        }

        var textFields = fields.Where(field => field.Kind == FieldKind.Text && !FillRules.IsSearchBox(field)).ToList();
        var isInForm = caret.FocusedFieldId is { } id && textFields.Exists(field => string.Equals(field.Id, id, StringComparison.Ordinal));
        return !isInForm || textFields.Count < 2;
    }

    /// <summary>
    /// Describes the cursor as a field: the words just before it are its label.
    /// </summary>
    /// <param name="caret">Where the cursor is.</param>
    /// <returns>A field labelled by the end of the current line, such as "mon mail :".</returns>
    public static FormField TargetField(CaretContext caret)
    {
        ArgumentNullException.ThrowIfNull(caret);

        var lastLine = caret.TextBefore.Replace('\r', '\n').Split('\n')[^1].Trim();
        var label = lastLine.Length > MaxLabelLength ? lastLine[^MaxLabelLength..].TrimStart() : lastLine;
        return new FormField
        {
            Id = caret.FocusedFieldId ?? CaretFieldId,
            Label = label,
            Hint = caret.FieldLabel,
            Kind = FieldKind.Text,
            HasValue = false,
        };
    }
}
