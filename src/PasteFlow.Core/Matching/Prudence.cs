namespace PasteFlow.Core.Matching;

/// <summary>
/// How sure the application must be to fill a field without asking.
/// </summary>
public enum Prudence
{
    /// <summary>
    /// Fills only the fields it is very sure about; more fields are left to check, with suggestions.
    /// </summary>
    Cautious,

    /// <summary>
    /// The default: cautious without being excessive.
    /// </summary>
    Balanced,

    /// <summary>
    /// Fills more fields, at the risk of more values to correct.
    /// </summary>
    Bold,
}
