namespace PasteFlow.Core.Forms;

/// <summary>
/// A field of the form displayed in the active window.
/// </summary>
/// <remarks>
/// The current value is deliberately not kept: only whether the field is already filled.
/// </remarks>
public sealed record FormField
{
    /// <summary>
    /// Gets the identifier of the field, unique within one reading of the form.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the label of the field as shown to the user, or an empty string if it has none.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the kind of input expected by the field.
    /// </summary>
    public required FieldKind Kind { get; init; }

    /// <summary>
    /// Gets a value indicating whether the field already contains a value.
    /// </summary>
    public required bool HasValue { get; init; }
}
