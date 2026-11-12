using System.Runtime.InteropServices;
using System.Windows.Automation;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads the address bar of a browser window: the site the user is on.
/// </summary>
/// <remarks>
/// Safety net: only the toolbars of the browser itself are read, never the page. A page could hold a field whose
/// value is "paypal.com", but it cannot change the address bar, which is what tells the real site.
/// </remarks>
internal static class BrowserAddress
{
    // The address bar sits a few levels under the window, far before the page and its thousands of elements
    private const int MaxDepth = 12;
    private const int MaxVisited = 600;

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
    };

    private static readonly Condition EditCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);

    // Chrome, Edge, Brave, Opera and Vivaldi draw their address bar with the same view
    private static readonly Condition OmniboxCondition = new AndCondition(
        EditCondition,
        new PropertyCondition(AutomationElement.ClassNameProperty, "OmniboxViewViews"));

    /// <summary>
    /// Reads the address shown by a browser window.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The address, such as "github.com/login", or an empty string if the window is not a known browser.</returns>
    public static string TryRead(AutomationElement window)
    {
        try
        {
            if (!IsBrowser(window.Current.ProcessId))
            {
                return string.Empty;
            }

            foreach (var toolBar in ToolBarsOutsidePages(window))
            {
                if (AddressIn(toolBar) is { Length: > 0 } address)
                {
                    return address;
                }
            }

            // Seen on 12/11/2026 on a sign-in page: the address bar was on screen but not found among the toolbars
            return window.FindFirst(TreeScope.Descendants, OmniboxCondition) is { } omnibox ? AddressOf(omnibox) : string.Empty;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // The window was closed or does not answer: no site known
        }

        return string.Empty;
    }

    private static string AddressIn(AutomationElement toolBar)
    {
        try
        {
            foreach (AutomationElement edit in toolBar.FindAll(TreeScope.Descendants, EditCondition))
            {
                if (AddressOf(edit) is { Length: > 0 } address)
                {
                    return address;
                }
            }
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // A toolbar changed while being read, such as a menu of an extension opening: the others are still read
        }

        return string.Empty;
    }

    private static string AddressOf(AutomationElement edit)
    {
        return edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern value
            && PageAddress.HostOf(value.Current.Value).Length > 0
            ? value.Current.Value.Trim()
            : string.Empty;
    }

    private static bool IsBrowser(int processId)
    {
        return BrowserProcesses.Contains(WindowProgram.NameOf(processId));
    }

    /// <summary>
    /// Finds the toolbars of the browser itself, without entering the pages: a page can hold thousands of elements,
    /// and its toolbars are not the address bar anyway.
    /// </summary>
    /// <param name="window">The window of the browser.</param>
    /// <returns>The toolbars, closest to the top of the window first.</returns>
    private static List<AutomationElement> ToolBarsOutsidePages(AutomationElement window)
    {
        var walker = TreeWalker.ControlViewWalker;
        var toolBars = new List<AutomationElement>();
        var level = new List<AutomationElement> { window };
        var visited = 0;
        for (var depth = 0; depth < MaxDepth && level.Count > 0 && visited < MaxVisited; depth++)
        {
            var next = new List<AutomationElement>();
            foreach (var parent in level)
            {
                try
                {
                    for (var child = walker.GetFirstChild(parent); child is not null && visited < MaxVisited; child = walker.GetNextSibling(child))
                    {
                        visited++;
                        var type = child.Current.ControlType;
                        if (type == ControlType.ToolBar)
                        {
                            toolBars.Add(child);
                        }
                        else if (type != ControlType.Document)
                        {
                            next.Add(child);
                        }
                    }
                }
                catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
                {
                    // An element vanished while being read, such as a popup closing: its neighbours are still read
                }
            }

            level = next;
        }

        return toolBars;
    }
}
