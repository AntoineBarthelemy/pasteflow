using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.History;

/// <summary>
/// A value written into a kind of field, with how often and how recently.
/// </summary>
public sealed record HistoryEntry
{
    /// <summary>
    /// Gets the label of the field the value was written into.
    /// </summary>
    public required string FieldLabel { get; init; }

    /// <summary>
    /// Gets the technical name of the field the value was written into.
    /// </summary>
    public string TechnicalName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the value written.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the kind of the value: its shape, or what the field expected.
    /// </summary>
    public CandidateKind Kind { get; init; } = CandidateKind.Text;

    /// <summary>
    /// Gets the number of times the value was written into this kind of field.
    /// </summary>
    public required int UseCount { get; init; }

    /// <summary>
    /// Gets the last time the value was written into this kind of field.
    /// </summary>
    public required DateTimeOffset LastUsed { get; init; }
}
