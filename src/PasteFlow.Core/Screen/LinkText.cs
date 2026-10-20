namespace PasteFlow.Core.Screen;

/// <summary>
/// A link of a window: the text the user sees and the address it leads to.
/// </summary>
public sealed record LinkText
{
    /// <summary>
    /// Gets the text of the link as shown, such as "Ma dernière vidéo".
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the address the link leads to, such as "https://www.youtube.com/watch?v=…".
    /// </summary>
    public required string Address { get; init; }
}
