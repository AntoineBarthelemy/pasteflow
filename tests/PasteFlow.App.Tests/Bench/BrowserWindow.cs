using System.Diagnostics;
using System.IO;
using System.Windows.Automation;

namespace PasteFlow.App.Tests.Bench;

/// <summary>
/// A page of the test bench opened in a real browser (Edge or Chrome) on its own profile, read and written through
/// UI Automation like any other application.
/// </summary>
internal sealed class BrowserWindow : IDisposable
{
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(200);

    private static readonly string[] BrowserPaths =
    {
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
    };

    private readonly Process _process;
    private readonly string _profile;

    private BrowserWindow(Process process, string profile, AutomationElement window)
    {
        _process = process;
        _profile = profile;
        Window = window;
    }

    /// <summary>
    /// Gets the browser installed on this computer, or <see langword="null"/> when there is none.
    /// </summary>
    public static string? BrowserPath { get; } = BrowserPaths.FirstOrDefault(File.Exists);

    /// <summary>
    /// Gets the window of the page.
    /// </summary>
    public AutomationElement Window { get; }

    /// <summary>
    /// Gets the handle of the window of the page.
    /// </summary>
    public IntPtr Handle => new(Window.Current.NativeWindowHandle);

    /// <summary>
    /// Opens a page in a new browser window, without address bar, and waits until it shows the given text.
    /// </summary>
    /// <param name="url">The page.</param>
    /// <param name="readyName">The name of an element the page shows once loaded, such as the label of a field.</param>
    /// <returns>The window.</returns>
    public static BrowserWindow Open(Uri url, string readyName)
    {
        var browser = BrowserPath ?? throw new InvalidOperationException("Aucun navigateur installé.");
        var profile = Path.Combine(Path.GetTempPath(), "pasteflow-bench-" + Guid.NewGuid().ToString("N"));
        var start = new ProcessStartInfo(browser)
        {
            ArgumentList =
            {
                $"--user-data-dir={profile}",
                "--no-first-run",
                "--no-default-browser-check",
                "--force-renderer-accessibility",
                "--disable-features=Translate",
                $"--app={url.AbsoluteUri}",
            },
            UseShellExecute = false,
        };
        var process = Process.Start(start) ?? throw new InvalidOperationException("Le navigateur n'a pas démarré.");

        var deadline = DateTime.UtcNow + OpenTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var window = FindWindow(process.Id);
            if (window?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, readyName)) is not null)
            {
                return new BrowserWindow(process, profile, window);
            }

            Thread.Sleep(PollDelay);
        }

        Kill(process, profile);
        throw new TimeoutException($"La page {url} n'a pas affiché « {readyName} ».");
    }

    /// <summary>
    /// Finds a text field of the page by its label.
    /// </summary>
    /// <param name="label">The label of the field.</param>
    /// <returns>The field.</returns>
    public AutomationElement Field(string label)
    {
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.NameProperty, label),
            new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox)));
        return Window.FindFirst(TreeScope.Descendants, condition)
            ?? throw new InvalidOperationException($"Pas de champ « {label} » dans la page.");
    }

    /// <summary>
    /// Reads the value of a field of the page.
    /// </summary>
    /// <param name="label">The label of the field.</param>
    /// <returns>The value, line breaks as \n.</returns>
    public string ValueOf(string label)
    {
        var field = Field(label);
        return field.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern value
            ? (value.Current.Value ?? string.Empty).ReplaceLineEndings("\n")
            : string.Empty;
    }

    public void Dispose()
    {
        Kill(_process, _profile);
    }

    private static AutomationElement? FindWindow(int processId)
    {
        // With its own profile, the process started is the browser itself, which owns the window
        var condition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        return AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
    }

    private static void Kill(Process process, string profile)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone
        }

        process.Dispose();
        try
        {
            Directory.Delete(profile, recursive: true);
        }
        catch (IOException)
        {
            // A file of the profile is still held: it stays in the temporary folder
        }
        catch (UnauthorizedAccessException)
        {
            // Same
        }
    }
}
