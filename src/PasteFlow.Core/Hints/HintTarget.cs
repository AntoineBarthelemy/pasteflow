using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Hints;

/// <summary>
/// A value seen on screen that can be pasted by typing its code.
/// </summary>
public sealed record HintTarget
{
    /// <summary>
    /// Gets the value pasted.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets what kind of value it is.
    /// </summary>
    public CandidateKind Kind { get; init; } = CandidateKind.Text;

    /// <summary>
    /// Gets the code of letters that pastes it, such as "FJ".
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the text on screen where the value is, whose top left corner holds the code.
    /// </summary>
    public required ScreenText Place { get; init; }
}
