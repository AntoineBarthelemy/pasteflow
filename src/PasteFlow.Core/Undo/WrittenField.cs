using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Undo;

/// <summary>
/// A value the application wrote into a field.
/// </summary>
public sealed record WrittenField
{
    /// <summary>
    /// Gets the field written.
    /// </summary>
    public required FormField Field { get; init; }

    /// <summary>
    /// Gets the value written.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the value the field held before, put back when the fill is undone; empty for a field that was empty.
    /// </summary>
    public string PreviousValue { get; init; } = string.Empty;
}
