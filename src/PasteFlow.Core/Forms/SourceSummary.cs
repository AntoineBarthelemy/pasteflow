namespace PasteFlow.Core.Forms;

/// <summary>
/// How many candidates a source offered during a fill.
/// </summary>
public sealed record SourceSummary
{
    /// <summary>
    /// Gets the name of the source, for example the title of a window.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the number of candidates offered by the source.
    /// </summary>
    public required int CandidateCount { get; init; }
}
