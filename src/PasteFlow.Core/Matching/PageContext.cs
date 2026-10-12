using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Matching;

/// <summary>
/// What the window being filled tells about the form: its title, the beginning of its text and, in a browser, the
/// site it shows.
/// </summary>
public sealed record PageContext
{
    /// <summary>
    /// The length of text kept: the headings that tell what the form is for come first.
    /// </summary>
    public const int MaxTextLength = 4000;

    /// <summary>
    /// Gets a context that tells nothing.
    /// </summary>
    public static PageContext Empty { get; } = new();

    /// <summary>
    /// Gets the title of the window.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the beginning of the text of the window.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Gets the host of the page shown, such as "github.com", or an empty string outside a browser.
    /// </summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the program of the window, such as "steam" or "chrome", or an empty string.
    /// </summary>
    public string Program { get; init; } = string.Empty;

    /// <summary>
    /// Gets the site of the page shown, such as "univ-reims.fr" for "moodle.univ-reims.fr".
    /// </summary>
    public string Site => PageAddress.SiteOf(Host);

    /// <summary>
    /// Gets the context of the window being filled.
    /// </summary>
    /// <param name="window">The text of the active window, or <see langword="null"/> if it could not be read.</param>
    /// <returns>The context.</returns>
    public static PageContext From(WindowText? window)
    {
        if (window is null)
        {
            return Empty;
        }

        return new PageContext
        {
            Title = window.Title,
            Text = window.Text.Length > MaxTextLength ? window.Text[..MaxTextLength] : window.Text,
            Host = PageAddress.HostOf(window.Address),
            Program = window.Program,
        };
    }
}
