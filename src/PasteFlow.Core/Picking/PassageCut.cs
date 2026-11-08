namespace PasteFlow.Core.Picking;

/// <summary>
/// How much text around the typed letters a passage takes.
/// </summary>
public enum PassageCut
{
    /// <summary>
    /// The typed words, completed to the end of the last word, values and units kept whole.
    /// </summary>
    Words,

    /// <summary>
    /// The whole sentence holding the typed words.
    /// </summary>
    Sentence,

    /// <summary>
    /// The whole line holding the typed words.
    /// </summary>
    Line,

    /// <summary>
    /// The whole bulleted or numbered list holding the typed words, every item on its line; the line alone when it
    /// is not part of a list.
    /// </summary>
    List,
}
