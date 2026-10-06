namespace PasteFlow.Core.Forms;

/// <summary>
/// One field in the recap of a fill.
/// </summary>
public sealed record RecapLine
{
    /// <summary>
    /// Gets what happened to the field.
    /// </summary>
    public required RecapLineKind Kind { get; init; }

    /// <summary>
    /// Gets the label of the field, shortened, or "Champ sans nom".
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Gets the value written, or a short state such as "à vérifier" when the field was not filled.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the name of the window the value comes from; empty when the field was not filled.
    /// </summary>
    public string SourceName { get; init; } = string.Empty;
}
