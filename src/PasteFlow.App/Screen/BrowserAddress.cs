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
                foreach (AutomationElement edit in toolBar.FindAll(TreeScope.Descendants, EditCondition))
                {
                    if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern value
                        && PageAddress.HostOf(value.Current.Value).Length > 0)
                    {
                        return value.Current.Value.Trim();
                    }
                }
            }
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // The window was closed or does not answer: no site known
        }

        return string.Empty;
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

            level = next;
        }

        return toolBars;
    }
}
