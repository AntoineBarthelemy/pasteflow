using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Picking;

/// <summary>
/// What the user can choose from when nothing is sure.
/// </summary>
public sealed record PickRequest
{
    /// <summary>
    /// Gets the most likely values, best first, offered before anything is typed.
    /// </summary>
    public required IReadOnlyList<Candidate> Suggestions { get; init; }

    /// <summary>
    /// Gets the search in the text of the windows read, used as soon as the user types.
    /// </summary>
    public required PassageFinder Finder { get; init; }

    /// <summary>
    /// Gets the addresses behind the links and the pages open behind, offered next to the passages found when the
    /// text of their link or the title of their page matches the search.
    /// </summary>
    public IReadOnlyList<Candidate> Links { get; init; } = Array.Empty<Candidate>();
}
