namespace PasteFlow.Core.Forms;

/// <summary>
/// A value proposed to the user for a field that could not be filled for sure.
/// </summary>
public sealed record SuggestedValue
{
    /// <summary>
    /// Gets the value proposed.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the name of the window the value comes from.
    /// </summary>
    public required string SourceName { get; init; }
}
