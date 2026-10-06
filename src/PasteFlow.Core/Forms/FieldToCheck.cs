namespace PasteFlow.Core.Forms;

/// <summary>
/// A field left to the user, with the values that could fit it, the most likely first.
/// </summary>
public sealed record FieldToCheck
{
    /// <summary>
    /// Gets the field.
    /// </summary>
    public required FormField Field { get; init; }

    /// <summary>
    /// Gets the values proposed for the field, the most likely first; empty when none is likely enough.
    /// </summary>
    public IReadOnlyList<SuggestedValue> Suggestions { get; init; } = Array.Empty<SuggestedValue>();
}
