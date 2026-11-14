using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Picking;

/// <summary>
/// Decides when the shortcut pastes at the cursor rather than filling a form, and what the cursor asks for.
/// </summary>
/// <remarks>
/// A form is a window with several fields to type into, a password field included: there, the shortcut fills the
/// form as a whole, so a sign-in page (an identifier and a password) is a form, not a lone text. A text written alone,
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

    // « voici son numéro » is a sentence; « Jean Martin » or an address typed by hand is a value
    private const int MinSentenceWords = 3;

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

        // The cell of a spreadsheet is no text box for Windows, but the user writes there (seen on 14/11/2026 in Excel)
        if (!caret.IsEditable && !caret.IsDocument)
        {
            return false;
        }

        // The page of a text editor or a word processor, seen on 14/11/2026 in Notepad and WordPad, or the grid of a
        // spreadsheet, seen in Excel: their toolbars and status bar hold boxes, but the user writes a text; a sign-in
        // page stays a form
        if (caret.IsDocument && !fields.Any(field => field.Kind == FieldKind.Password))
        {
            return true;
        }

        // A password field counts: an identifier alone next to it is a sign-in form, filled as a whole
        var textFields = fields
            .Where(field => (field.Kind == FieldKind.Text && !FillRules.IsSearchBox(field)) || field.Kind == FieldKind.Password)
            .ToList();
        var isInForm = caret.FocusedFieldId is { } id && textFields.Exists(field => string.Equals(field.Id, id, StringComparison.Ordinal));
        if (!isInForm || textFields.Count < 2)
        {
            return true;
        }

        // A sentence being written in a box among others, as « Bonjour, voici son numéro de suivi : » in a message
        // next to an internal note: the words before the cursor ask for a value (decision of 10/11/2026). A sign-in
        // page and a box left empty or holding a short value stay a form, filled as a whole
        return !textFields.Exists(field => field.Kind == FieldKind.Password) && IsWritingASentence(caret.TextBefore);
    }

    /// <summary>
    /// Determines whether the text before the cursor is a sentence being written, not a value typed in a field.
    /// </summary>
    /// <param name="textBefore">The text of the field before the cursor.</param>
    /// <returns><see langword="true"/> from three words on; otherwise <see langword="false"/>.</returns>
    public static bool IsWritingASentence(string textBefore)
    {
        ArgumentNullException.ThrowIfNull(textBefore);

        return textBefore
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Count(word => word.Any(char.IsLetter)) >= MinSentenceWords;
    }

    /// <summary>
    /// Determines whether a value was only read after the words being typed: the active window shows them too, and
    /// what follows them on screen (often the button under the box) is not a value they ask for.
    /// </summary>
    /// <param name="candidate">A value found.</param>
    /// <param name="target">The cursor described as a field, see <see cref="TargetField"/>.</param>
    /// <returns><see langword="true"/> to set the value aside; otherwise <see langword="false"/>.</returns>
    public static bool IsTypedText(Candidate candidate, FormField target)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(target);

        var label = WithoutColon(target.Label);
        return candidate.SourceRank == 0
            && !candidate.IsFromHistory
            && !candidate.IsFromProfile
            && label.Length > 0
            && string.Equals(WithoutColon(candidate.Label), label, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// Describes the cursor of a window without any field, such as a terminal: the words of the element, if it can be
    /// read, tell what the list should rank first.
    /// </summary>
    /// <param name="caret">Where the cursor is, or <see langword="null"/> when it cannot be read.</param>
    /// <returns>A field standing for the cursor.</returns>
    public static FormField NoFieldTarget(CaretContext? caret)
    {
        return caret is null
            ? new FormField { Id = CaretFieldId, Label = string.Empty, Kind = FieldKind.Text, HasValue = false }
            : TargetField(caret) with { Id = CaretFieldId };
    }

    private static string WithoutColon(string label)
    {
        return label.Trim().TrimEnd(':', '：').TrimEnd();
    }
}
