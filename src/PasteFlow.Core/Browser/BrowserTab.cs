using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Browser;

/// <summary>
/// A tab of a browser read by the extension of PasteFlow, even when it is hidden behind another tab.
/// </summary>
public sealed record BrowserTab
{
    /// <summary>
    /// Gets the title of the tab.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the address of the page.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets the text the page shows, lines separated by line breaks.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the links of the page, each with its text and its address.
    /// </summary>
    public IReadOnlyList<LinkText> Links { get; init; } = Array.Empty<LinkText>();

    /// <summary>
    /// Gets when the user last looked at the tab, in milliseconds since 1970, or 0 if the browser does not tell.
    /// </summary>
    public double LastAccessed { get; init; }
}
