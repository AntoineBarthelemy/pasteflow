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
}
