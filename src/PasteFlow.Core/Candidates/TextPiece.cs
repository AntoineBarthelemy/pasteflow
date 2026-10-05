namespace PasteFlow.Core.Candidates;

/// <summary>
/// A unit of meaning found in a text: a value, its kind and the label that introduces it.
/// </summary>
public sealed record TextPiece
{
    /// <summary>
    /// Gets the value, for example "CMD-48213".
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the kind of value recognized.
    /// </summary>
    public required CandidateKind Kind { get; init; }

    /// <summary>
    /// Gets the label that introduces the value, for example "Commande n°", or an empty string if there is none.
    /// </summary>
    public required string Label { get; init; }
}
