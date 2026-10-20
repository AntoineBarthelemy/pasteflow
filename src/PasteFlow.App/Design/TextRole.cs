namespace PasteFlow.App.Design;

/// <summary>
/// The kinds of text of PasteFlow, from the title of a window to the smallest hint.
/// </summary>
internal enum TextRole
{
    /// <summary>The title of a window, in serif.</summary>
    Title,

    /// <summary>The title of a section or a popup.</summary>
    Heading,

    /// <summary>The main text.</summary>
    Body,

    /// <summary>A description, in a softer color.</summary>
    Secondary,

    /// <summary>A small text, such as a label or a source.</summary>
    Caption,

    /// <summary>The smallest hint, barely there.</summary>
    Hint,
}
