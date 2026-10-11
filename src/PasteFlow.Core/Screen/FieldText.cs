namespace PasteFlow.Core.Screen;

/// <summary>
/// A filled field of a window, with its label: a value another form can take as it is.
/// </summary>
public sealed record FieldText
{
    /// <summary>
    /// Gets the label of the field, such as "Title (required)".
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the whole value of the field, line breaks included.
    /// </summary>
    public required string Value { get; init; }
}
