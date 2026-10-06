namespace PasteFlow.Core.Forms;

/// <summary>
/// What happened to a field, as shown in the recap of a fill.
/// </summary>
public enum RecapLineKind
{
    /// <summary>
    /// The field was filled.
    /// </summary>
    Filled,

    /// <summary>
    /// The field was left empty because no value was sure enough.
    /// </summary>
    ToCheck,

    /// <summary>
    /// A value was chosen but the field refused it.
    /// </summary>
    Failed,
}
