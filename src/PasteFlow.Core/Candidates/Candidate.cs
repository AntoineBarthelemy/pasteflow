namespace PasteFlow.Core.Candidates;

/// <summary>
/// A value that could be pasted into a field, with where it comes from.
/// </summary>
public sealed record Candidate
{
    /// <summary>
    /// Gets the value that could be pasted.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets a short name of where the value was found, for example the title of a window.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Gets the closeness of the source to the user: 0 for the closest one, higher values for older or farther sources.
    /// </summary>
    public required int SourceRank { get; init; }
}
