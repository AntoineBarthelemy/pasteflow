namespace PasteFlow.Core.Forms;

/// <summary>
/// A value written into a field, with where it was found.
/// </summary>
public sealed record FilledValue
{
    /// <summary>
    /// Gets the label of the field filled.
    /// </summary>
    public required string FieldLabel { get; init; }

    /// <summary>
    /// Gets the value written.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the name of the source of the value, for example the title of a window.
    /// </summary>
    public required string SourceName { get; init; }
}
