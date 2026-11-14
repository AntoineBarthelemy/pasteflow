using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Automation;
using PasteFlow.App.Forms;
using PasteFlow.App.Screen;

namespace PasteFlow.App.Tests.Forms;

/// <summary>
/// Real software of each family that the web test bench imitates (directive of 13/11/2026): what PasteFlow reads in
/// them and where it sees a place to write, with fake data only. Seen on 14/11/2026: the status bar of Notepad and
/// the font boxes of WordPad were taken for the fields of a form.
/// </summary>
public sealed class RealApplicationsTests
{
    private const string Text = "Commande : CMD-4471\r\nE-mail : helene.moreau@moreau-conseil.fr";
    private const string WordPadPath = @"C:\Program Files\Windows NT\Accessories\wordpad.exe";
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(20);

    [SkippableFact]
    public async Task Notepad_ItsStatusBarIsNoFieldAndItsPageFillsTheWindow()
    {
        var notepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        Skip.IfNot(File.Exists(notepad), "Pas de Bloc-notes sur cette machine.");

        using var app = RealApp.Open(notepad, FakeFile("bloc-notes"));
        Skip.If(app.Window is null, "Le Bloc-notes ne s'est pas ouvert.");

        var fields = await new UiAutomationFormAccess().ReadFormAsync(app.Handle, CancellationToken.None);

        // The status bar (« Ln 1, Col 1 », « UTF-8 ») shows values but takes none: it is no field of a form
        Assert.True(fields.Count == 0, $"Champs lus : {string.Join(" ; ", fields.Select(field => $"{field.Kind} « {field.Label} »"))} (fenêtre « {app.Window!.Current.Name} »)");
        Assert.Contains("CMD-4471", UiAutomationWindowTextReader.ReadWindow(app.Handle, 1)?.Text ?? string.Empty, StringComparison.Ordinal);
        Assert.True(UiAutomationFormAccess.FillsItsWindow(app.Page(ControlType.Document)!));
    }

    [SkippableFact]
    public void WordPad_ItsPageFillsTheWindowWhateverTheToolbarBoxes()
    {
        Skip.IfNot(File.Exists(WordPadPath), "WordPad n'est pas installé sur cette machine.");

        using var app = RealApp.Open(WordPadPath, FakeFile("wordpad"));
        Skip.If(app.Window is null, "WordPad ne s'est pas ouvert.");

        var page = app.Page(ControlType.Document);
        Assert.NotNull(page);
        Assert.True(UiAutomationFormAccess.IsEditable(page!));
        Assert.True(UiAutomationFormAccess.FillsItsWindow(page!));
    }

    [SkippableFact]
    public async Task PowerShellConsole_HasNoFieldAndItsTextIsRead()
    {
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        Skip.IfNot(File.Exists(powershell), "Pas de PowerShell sur cette machine.");

        var file = FakeFile("console");
        using var app = RealApp.Open(powershell, "-NoExit", "-Command", $"Get-Content -Encoding UTF8 '{file}'");
        Skip.If(app.Window is null, "La console ne s'est pas ouverte.");

        var fields = await new UiAutomationFormAccess().ReadFormAsync(app.Handle, CancellationToken.None);

        Assert.Empty(fields);
        var deadline = DateTime.UtcNow + OpenTimeout;
        var text = string.Empty;
        while (!text.Contains("CMD-4471", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(300);
            text = UiAutomationWindowTextReader.ReadWindow(app.Handle, 1)?.Text ?? string.Empty;
        }

        Assert.Contains("helene.moreau@moreau-conseil.fr", text, StringComparison.Ordinal);
    }

    private static string FakeFile(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pasteflow-reel-{name}-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    /// <summary>
    /// A real application opened on fake data, closed at the end.
    /// </summary>
    private sealed class RealApp : IDisposable
    {
        private readonly Process _process;

        private RealApp(Process process, AutomationElement? window)
        {
            _process = process;
            Window = window;
        }

        public AutomationElement? Window { get; }

        public IntPtr Handle => Window is null ? IntPtr.Zero : new IntPtr(Window.Current.NativeWindowHandle);

        public static RealApp Open(string path, params string[] arguments)
        {
            var start = new ProcessStartInfo(path) { UseShellExecute = true };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            var process = Process.Start(start) ?? throw new InvalidOperationException($"{path} n'a pas démarré.");
            var deadline = DateTime.UtcNow + OpenTimeout;
            AutomationElement? window = null;
            while (window is null && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(300);
                window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));

                // A console window belongs to its host, not to the program it runs
                process.Refresh();
                if (window is null && process.MainWindowHandle != IntPtr.Zero)
                {
                    window = AutomationElement.FromHandle(process.MainWindowHandle);
                }
            }

            // The page of a document is ready a moment after its window
            Thread.Sleep(1000);
            return new RealApp(process, window);
        }

        public AutomationElement? Page(ControlType type)
        {
            return Window?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        }

        public void Dispose()
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _ = _process.WaitForExit(5000);
            }
            catch (InvalidOperationException)
            {
                // Already closed
            }

            _process.Dispose();
        }
    }
}
