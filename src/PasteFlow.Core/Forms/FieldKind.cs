namespace PasteFlow.Core.Forms;

/// <summary>
/// Kind of input expected by a form field.
/// </summary>
public enum FieldKind
{
    /// <summary>
    /// A free text field.
    /// </summary>
    Text,

    /// <summary>
    /// A password field, whose value is never read.
    /// </summary>
    Password,

    /// <summary>
    /// A list of choices, such as a drop-down list.
    /// </summary>
    Choice,
}
