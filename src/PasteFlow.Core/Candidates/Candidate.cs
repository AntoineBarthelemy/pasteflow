namespace PasteFlow.Core.Candidates;

/// <summary>
/// A value that could be pasted into a field, with what it is and where it comes from.
/// </summary>
public sealed record Candidate
{
    /// <summary>
    /// Gets the value that could be pasted.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the kind of value recognized.
    /// </summary>
    public CandidateKind Kind { get; init; } = CandidateKind.Text;

    /// <summary>
    /// Gets the label that introduces the value where it was found, or an empty string if there is none.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Gets a short name of where the value was found, for example the title of a window.
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Gets the closeness of the source to the user: 0 for the closest one, higher values for older or farther sources.
    /// </summary>
    public required int SourceRank { get; init; }

    /// <summary>
    /// Gets a value indicating whether the value comes from the history of fills rather than from the screen:
    /// such values only fill the fields the screen says nothing about.
    /// </summary>
    public bool IsFromHistory { get; init; }

    /// <summary>
    /// Gets a value indicating whether the value was typed in a field of another form: a whole value someone
    /// entered, which may contain words such as "votre" or "exemple" without being an example text.
    /// </summary>
    public bool IsTypedInField { get; init; }

    /// <summary>
    /// Gets the line of the person or company the value belongs to where it was found, such as "Léa Martin,
    /// responsable communication" for the email written under it, or an empty string if none is known.
    /// </summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the value was read from the image of a window (OCR): a character may be misread,
    /// so such a value is only proposed, never written without the user.
    /// </summary>
    public bool IsReadFromImage { get; init; }
}
