using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Matching;

/// <summary>
/// The value chosen for a field, with how sure the application is about it.
/// </summary>
public sealed record FieldMatch
{
    /// <summary>
    /// Gets the field to fill.
    /// </summary>
    public required FormField Field { get; init; }

    /// <summary>
    /// Gets the best candidate found for the field, or <see langword="null"/> if no candidate fits at all.
    /// </summary>
    public Candidate? Candidate { get; init; }

    /// <summary>
    /// Gets the score of the best candidate; the higher, the more likely.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Gets a value indicating whether the application is sure enough to fill the field without asking.
    /// </summary>
    public bool IsConfident { get; init; }
}
