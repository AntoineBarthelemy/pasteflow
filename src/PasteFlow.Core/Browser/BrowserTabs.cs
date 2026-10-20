using System.Text.Json;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Browser;

/// <summary>
/// The messages exchanged with the extension of PasteFlow, and how the tabs it reads join the windows read on screen.
/// </summary>
/// <remarks>
/// <para>PasteFlow asks for the tabs only when the user presses a shortcut; the extension answers with the most
/// recently viewed ones. Nothing is read in the background, and nothing leaves the computer.</para>
/// <para>A tab already seen on screen, such as the page being filled or a window behind, is left to the reading of the
/// screen: it keeps its place among the windows.</para>
/// </remarks>
public static class BrowserTabs
{
    /// <summary>
    /// The number of tabs read at most, the most recently viewed first.
    /// </summary>
    public const int MaxTabs = 5;

    /// <summary>
    /// The longest text kept for a tab.
    /// </summary>
    public const int MaxTextLength = 100_000;

    /// <summary>
    /// The largest number of links kept for a tab.
    /// </summary>
    public const int MaxLinks = 300;

    /// <summary>
    /// Writes the request sent to the extension.
    /// </summary>
    /// <param name="id">The number of the request, given back in the answer.</param>
    /// <returns>The JSON text of the request.</returns>
    public static string Request(int id)
    {
        return JsonSerializer.Serialize(new { type = "read", id, max = MaxTabs });
    }

    /// <summary>
    /// Reads the number of the request an answer of the extension replies to.
    /// </summary>
    /// <param name="json">The answer.</param>
    /// <returns>The number, or <see langword="null"/> if the message is not an answer with tabs.</returns>
    public static int? AnswerId(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "tabs"
                && root.TryGetProperty("id", out var id) && id.TryGetInt32(out var number)
                ? number
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads the tabs of an answer of the extension, ignoring anything malformed.
    /// </summary>
    /// <param name="json">The answer.</param>
    /// <returns>The tabs, the most recently viewed first, at most <see cref="MaxTabs"/>.</returns>
    public static IReadOnlyList<BrowserTab> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var tabs = new List<BrowserTab>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("tabs", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return tabs;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && TabOf(item) is { } tab)
                {
                    tabs.Add(tab);
                }
            }
        }
        catch (JsonException)
        {
            // A broken answer gives no tab
        }

        return tabs.OrderByDescending(tab => tab.LastAccessed).Take(MaxTabs).ToList();
    }

    /// <summary>
    /// Adds the tabs to the windows read on screen, after them, leaving out the tabs already seen on screen.
    /// </summary>
    /// <param name="windows">The windows read on screen.</param>
    /// <param name="tabs">The tabs read by the extension, the most recently viewed first.</param>
    /// <returns>The windows, then the tabs as windows further behind.</returns>
    public static IReadOnlyList<WindowText> Merge(IReadOnlyList<WindowText> windows, IReadOnlyList<BrowserTab> tabs)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(tabs);

        var merged = windows.ToList();
        var nextRank = windows.Count == 0 ? 0 : windows.Max(window => window.Rank) + 1;
        foreach (var tab in tabs.Take(MaxTabs))
        {
            // A browser window is titled after its visible tab, such as "Gmail - Google Chrome"
            var title = tab.Title.Trim();
            var isOnScreen = title.Length > 0 && windows.Any(window => window.Title.StartsWith(title, StringComparison.Ordinal));
            if (isOnScreen || tab.Text.Trim().Length == 0)
            {
                continue;
            }

            merged.Add(new WindowText
            {
                Title = title.Length > 0 ? title : tab.Url,
                Text = tab.Text,
                Rank = nextRank++,
                Address = tab.Url,
                Links = tab.Links,
            });
        }

        return merged;
    }

    private static BrowserTab? TabOf(JsonElement item)
    {
        var title = StringOf(item, "title");
        var text = StringOf(item, "text");
        if (title is null && text is null)
        {
            return null;
        }

        var links = new List<LinkText>();
        if (item.TryGetProperty("links", out var linkItems) && linkItems.ValueKind == JsonValueKind.Array)
        {
            foreach (var link in linkItems.EnumerateArray().Take(MaxLinks))
            {
                if (link.ValueKind == JsonValueKind.Object && StringOf(link, "text") is { Length: > 0 } linkText && StringOf(link, "href") is { Length: > 0 } href)
                {
                    links.Add(new LinkText { Text = linkText.Trim(), Address = href.Trim() });
                }
            }
        }

        var lastAccessed = item.TryGetProperty("lastAccessed", out var time) && time.ValueKind == JsonValueKind.Number ? time.GetDouble() : 0;
        text ??= string.Empty;
        return new BrowserTab
        {
            Title = title ?? string.Empty,
            Url = StringOf(item, "url") ?? string.Empty,
            Text = text.Length > MaxTextLength ? text[..MaxTextLength] : text,
            Links = links,
            LastAccessed = lastAccessed,
        };
    }

    private static string? StringOf(JsonElement item, string name)
    {
        return item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
}
