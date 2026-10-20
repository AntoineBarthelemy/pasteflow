using System.Runtime.InteropServices;
using System.Windows.Automation;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads what a window behind holds without showing it as text: the address of the page open in a browser and the
/// addresses behind its links.
/// </summary>
internal static class WindowLinks
{
    /// <summary>
    /// The largest number of links read in a window.
    /// </summary>
    internal const int MaxLinks = 300;

    private static readonly Condition LinkCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink);

    /// <summary>
    /// Reads the address of the page and the links of a window.
    /// </summary>
    /// <param name="handle">The window.</param>
    /// <returns>The address of the page, empty if the window is not a browser, and the links with a text and an address.</returns>
    public static (string Address, IReadOnlyList<LinkText> Links) Read(IntPtr handle)
    {
        var links = new List<LinkText>();
        var address = string.Empty;
        try
        {
            var window = AutomationElement.FromHandle(handle);
            address = BrowserAddress.TryRead(window);

            var request = new CacheRequest();
            request.Add(AutomationElement.NameProperty);
            request.Add(ValuePattern.ValueProperty);
            AutomationElementCollection elements;
            using (request.Activate())
            {
                elements = window.FindAll(TreeScope.Descendants, LinkCondition);
            }

            foreach (AutomationElement element in elements)
            {
                if (links.Count == MaxLinks)
                {
                    break;
                }

                // The text of a link is its name; browsers give the address it leads to as its value
                if (element.GetCachedPropertyValue(AutomationElement.NameProperty) is string text && !string.IsNullOrWhiteSpace(text)
                    && element.GetCachedPropertyValue(ValuePattern.ValueProperty, ignoreDefaultValue: true) is string target
                    && !string.IsNullOrWhiteSpace(target))
                {
                    links.Add(new LinkText { Text = text.Trim(), Address = target.Trim() });
                }
            }
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            // The window was closed or does not answer: what was read is kept
        }

        return (address, links);
    }
}
