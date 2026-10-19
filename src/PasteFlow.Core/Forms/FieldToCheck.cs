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

    /// <summary>
    /// Gets why the field is left to the user, in a sentence for them, such as "51500 ne correspond pas à Reims selon
    /// La Poste.", or an empty string when it is not known.
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}
