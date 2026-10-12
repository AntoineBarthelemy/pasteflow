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
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
    };

    private static readonly Condition ToolBarCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar);

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

            foreach (AutomationElement toolBar in window.FindAll(TreeScope.Descendants, ToolBarCondition))
            {
                if (IsInsidePage(toolBar, window))
                {
                    continue;
                }

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

    private static bool IsInsidePage(AutomationElement element, AutomationElement window)
    {
        var walker = TreeWalker.ControlViewWalker;
        for (var parent = walker.GetParent(element); parent is not null && !Automation.Compare(parent, window); parent = walker.GetParent(parent))
        {
            if (parent.Current.ControlType == ControlType.Document)
            {
                return true;
            }
        }

        return false;
    }
}
