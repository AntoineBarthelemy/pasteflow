using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;

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

    /// <summary>
    /// Gets what the window written into told: whether its form is about the user, and on which site.
    /// </summary>
    public PageContext Page { get; init; } = PageContext.Empty;
}
