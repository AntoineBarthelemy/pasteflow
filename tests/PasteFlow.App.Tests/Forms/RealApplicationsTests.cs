using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Automation;
using PasteFlow.App.Forms;
using PasteFlow.App.Screen;
using PasteFlow.Core.Picking;

namespace PasteFlow.App.Tests.Forms;

/// <summary>
/// Real software of each family that the web test bench imitates (directive of 13/11/2026): what PasteFlow reads in
/// them and where it sees a place to write, with fake data only. Seen on 14/11/2026: the status bar of Notepad and
/// the font boxes of WordPad were taken for the fields of a form, then the ribbon boxes of Excel. A program the user
/// already opened is never used nor closed: the check is skipped.
/// </summary>
public sealed class RealApplicationsTests
{
    private const string Text = "Commande : CMD-4471\r\nE-mail : helene.moreau@moreau-conseil.fr";
    private const string WordPadPath = @"C:\Program Files\Windows NT\Accessories\wordpad.exe";
    private const string LibreOfficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";
    private const string OfficePath = @"C:\Program Files\Microsoft Office\root\Office16";
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

        // The console writes its lines one after the other: the last one is awaited (16/11/2026, read between two lines)
        var deadline = DateTime.UtcNow + OpenTimeout;
        var text = string.Empty;
        while (!text.Contains("helene.moreau@moreau-conseil.fr", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(300);
            text = UiAutomationWindowTextReader.ReadWindow(app.Handle, 1)?.Text ?? string.Empty;
        }

        Assert.Contains("helene.moreau@moreau-conseil.fr", text, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Word_ItsPageIsADocumentPastedAtTheCursor()
    {
        var word = Path.Combine(OfficePath, "WINWORD.EXE");
        Skip.IfNot(File.Exists(word), "Word n'est pas installé sur cette machine.");
        Skip.If(Process.GetProcessesByName("WINWORD").Length > 0, "Word est déjà ouvert : il n'est pas touché.");

        using var app = RealApp.OpenWindow(word, "fiche", "/q", FakeRtf("word"));
        Skip.If(app.Window is null, "Word ne s'est pas ouvert.");

        var page = app.Page(ControlType.Document);
        Assert.NotNull(page);
        var access = new UiAutomationFormAccess();
        var fields = await access.ReadFormAsync(app.Handle, CancellationToken.None);
        Assert.True(FreePaste.IsFreeText(fields, access.DescribeCaret(page!)), $"Champs lus : {string.Join(" ; ", fields.Select(field => $"« {field.Label} »"))}");
        Assert.Contains("CMD-4471", UiAutomationWindowTextReader.ReadWindow(app.Handle, 1)?.Text ?? string.Empty, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Excel_ItsGridIsPastedAtTheCursorWhateverTheRibbonBoxes()
    {
        var excel = Path.Combine(OfficePath, "EXCEL.EXE");
        Skip.IfNot(File.Exists(excel), "Excel n'est pas installé sur cette machine.");
        Skip.If(Process.GetProcessesByName("EXCEL").Length > 0, "Excel est déjà ouvert : il n'est pas touché.");

        using var app = RealApp.OpenWindow(excel, "commandes", "/x", FakeCsv("excel", "Reims"));
        Skip.If(app.Window is null, "Excel ne s'est pas ouvert.");

        // The grid shows a moment after the window, later at the first start of Excel
        var gridCondition = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid),
            new PropertyCondition(AutomationElement.ClassNameProperty, "XLSpreadsheetGrid"));
        var deadline = DateTime.UtcNow + OpenTimeout;
        var grid = app.Window!.FindFirst(TreeScope.Descendants, gridCondition);
        while (grid is null && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            grid = app.Window.FindFirst(TreeScope.Descendants, gridCondition);
        }

        Assert.NotNull(grid);
        Assert.True(UiAutomationFormAccess.IsInSpreadsheetGrid(grid!));

        // The ribbon boxes (name box, formula bar, font) are read as fields, the cell is no text box: before the fix of
        // 14/11/2026, nothing was pasted
        var access = new UiAutomationFormAccess();
        var fields = await access.ReadFormAsync(app.Handle, CancellationToken.None);
        Assert.True(FreePaste.IsFreeText(fields, access.DescribeCaret(grid!)), $"Champs lus : {string.Join(" ; ", fields.Select(field => $"« {field.Label} »"))}");

        // Each cell is read whole, row by row, though the city next to it cuts the e-mail on screen: before the fix of
        // 16/11/2026, the text shown by each cell was read, « helene.more », and the grid was not
        var ocr = OcrTextRecognizer.TryCreate();

        // A busy computer may answer late: the reading is tried again, as a user would press the shortcut again
        var reader = new UiAutomationWindowTextReader(ocr);
        var reading = await reader.ReadWithFallbackAsync(app.Handle, 1, CancellationToken.None);
        var readingDeadline = DateTime.UtcNow + OpenTimeout;
        while (reading?.Text.Contains("CMD-4471", StringComparison.Ordinal) != true && DateTime.UtcNow < readingDeadline)
        {
            Thread.Sleep(1000);
            reading = await reader.ReadWithFallbackAsync(app.Handle, 1, CancellationToken.None);
        }

        Assert.NotNull(reading);
        Assert.False(reading!.IsReadFromImage);
        Assert.Contains("CMD-4471\thelene.moreau@moreau-conseil.fr\tReims", reading.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(reading.Text.Split('\n'), line => line.Trim() == "helene.more");
    }

    [SkippableFact]
    public async Task WindowsTerminal_HasNoFieldAndItsTextIsRead()
    {
        var terminal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        Skip.IfNot(File.Exists(terminal), "Windows Terminal n'est pas installé sur cette machine.");
        Skip.If(Process.GetProcessesByName("WindowsTerminal").Length > 0, "Windows Terminal est déjà ouvert : il n'est pas touché.");

        var file = FakeFile("terminal");
        using var app = RealApp.OpenWindow(terminal, "CASCADIA_HOSTING_WINDOW_CLASS", "powershell", "-NoExit", "-Command", $"Get-Content -Encoding UTF8 '{file}'");
        Skip.If(app.Window is null, "Windows Terminal ne s'est pas ouvert.");

        var fields = await new UiAutomationFormAccess().ReadFormAsync(app.Handle, CancellationToken.None);

        Assert.Empty(fields);
        var deadline = DateTime.UtcNow + OpenTimeout;
        var text = string.Empty;
        while (!text.Contains("helene.moreau@moreau-conseil.fr", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(300);
            text = UiAutomationWindowTextReader.ReadWindow(app.Handle, 1)?.Text ?? string.Empty;
        }

        Assert.Contains("CMD-4471", text, StringComparison.Ordinal);
        Assert.Contains("helene.moreau@moreau-conseil.fr", text, StringComparison.Ordinal);
    }

    [SkippableTheory]
    [InlineData("writer")]
    [InlineData("calc")]
    public async Task LibreOffice_GivesNoTextButItsPictureIsReadRight(string module)
    {
        // LibreOffice gives Windows its menus only, not the text of its pages and cells: its picture is read, which
        // misread the words under the red line of the spell checker until 15/11/2026 (« SMp-4471 »)
        Skip.IfNot(File.Exists(LibreOfficePath), "LibreOffice n'est pas installé sur cette machine.");

        // LibreOffice ends a few seconds after the window of the previous check
        var closing = DateTime.UtcNow + OpenTimeout;
        while (Process.GetProcessesByName("soffice.bin").Length > 0 && DateTime.UtcNow < closing)
        {
            Thread.Sleep(500);
        }

        Skip.If(Process.GetProcessesByName("soffice.bin").Length > 0, "LibreOffice est déjà ouvert : il n'est pas touché.");
        var ocr = OcrTextRecognizer.TryCreate();
        Skip.If(ocr is null, "Pas de reconnaissance de caractères pour les langues de cette machine.");

        using var app = module == "writer"
            ? RealApp.OpenWindow(LibreOfficePath, "fiche", "--nologo", "--norestore", "--writer", FakeRtf("writer"))
            : RealApp.OpenWindow(LibreOfficePath, "commandes", "--nologo", "--norestore", "--infilter=CSV:59,34,76,1", "--calc", FakeCsv("calc"));
        Skip.If(app.Window is null, "LibreOffice ne s'est pas ouvert.");

        // Its page is drawn a moment after its window, later when the computer is busy
        var reader = new UiAutomationWindowTextReader(ocr);
        var reading = await reader.ReadWithFallbackAsync(app.Handle, 1, CancellationToken.None);
        var deadline = DateTime.UtcNow + OpenTimeout;
        while (reading?.Text.Contains("helene.moreau@moreau-conseil.fr", StringComparison.Ordinal) != true && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1000);
            reading = await reader.ReadWithFallbackAsync(app.Handle, 1, CancellationToken.None);
        }

        Assert.NotNull(reading);
        Assert.True(reading!.IsReadFromImage);
        Assert.Contains("CMD-4471", reading.Text, StringComparison.Ordinal);
        Assert.Contains("helene.moreau@moreau-conseil.fr", reading.Text, StringComparison.Ordinal);
    }

    private static string FakeFile(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pasteflow-reel-{name}-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    private static string FakeRtf(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pasteflow-reel-{name}-{Guid.NewGuid():N}", "fiche.rtf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, @"{\rtf1\ansi\deff0{\fonttbl{\f0 Calibri;}}\f0 Commande : CMD-4471\par E-mail : helene.moreau@moreau-conseil.fr\par}", Encoding.ASCII);
        return path;
    }

    private static string FakeCsv(string name, string? thirdColumn = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pasteflow-reel-{name}-{Guid.NewGuid():N}", "commandes.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var header = thirdColumn is null ? "Commande;E-mail" : "Commande;E-mail;Ville";
        var row = thirdColumn is null ? "CMD-4471;helene.moreau@moreau-conseil.fr" : $"CMD-4471;helene.moreau@moreau-conseil.fr;{thirdColumn}";
        File.WriteAllText(path, $"{header}\r\n{row}\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    /// <summary>
    /// A real application opened on fake data, closed at the end.
    /// </summary>
    private sealed class RealApp : IDisposable
    {
        private readonly Process _process;
        private bool _closeByWindow;

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

        /// <summary>
        /// Opens a program whose window does not belong to the process started (Office hands its document to its own
        /// window, Windows Terminal to its host), found by a part of its title or its class.
        /// </summary>
        /// <param name="path">The program.</param>
        /// <param name="titleOrClass">A part of the title of the window, or its class.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The application, closed by its window only.</returns>
        public static RealApp OpenWindow(string path, string titleOrClass, params string[] arguments)
        {
            var before = TopWindows().Select(window => window.Current.NativeWindowHandle).ToHashSet();
            var start = new ProcessStartInfo(path) { UseShellExecute = true };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            var process = Process.Start(start) ?? throw new InvalidOperationException($"{path} n'a pas démarré.");

            // Office starts slowly the first time
            var deadline = DateTime.UtcNow + (OpenTimeout * 4);
            AutomationElement? window = null;
            while (window is null && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
                window = TopWindows().FirstOrDefault(candidate => IsNewWindow(candidate, before, titleOrClass));
            }

            Thread.Sleep(3000);
            return new RealApp(process, window) { _closeByWindow = true };
        }

        public AutomationElement? Page(ControlType type)
        {
            return Window?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        }

        public void Dispose()
        {
            if (_closeByWindow)
            {
                CloseWindow();
                _process.Dispose();
                return;
            }

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

        private static bool IsNewWindow(AutomationElement candidate, HashSet<int> before, string titleOrClass)
        {
            try
            {
                var current = candidate.Current;
                return !before.Contains(current.NativeWindowHandle)
                    && current.ClassName != "MsoSplash"
                    && (current.Name.Contains(titleOrClass, StringComparison.OrdinalIgnoreCase) || current.ClassName == titleOrClass);
            }
            catch (ElementNotAvailableException)
            {
                // A window closing while the windows are read, such as a splash screen: the program would stay open
                // after the test (seen with Excel on 16/11/2026)
                return false;
            }
        }

        private static List<AutomationElement> TopWindows()
        {
            return AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition).Cast<AutomationElement>().ToList();
        }

        // Only this window is closed, as the user would: Word killed asks for its safe mode at the next start, and the
        // process of Windows Terminal may hold other windows
        private void CloseWindow()
        {
            if (Window is null)
            {
                return;
            }

            try
            {
                var processId = Window.Current.ProcessId;
                if (Window.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern))
                {
                    ((WindowPattern)pattern).Close();
                }

                var deadline = DateTime.UtcNow + OpenTimeout;
                while (DateTime.UtcNow < deadline && TopWindows().Exists(window => window.Current.ProcessId == processId && window.Current.Name.Length > 0))
                {
                    Thread.Sleep(500);
                }

                // The program ends a moment after its window: the next check must not find it still open
                using var owner = Process.GetProcessById(processId);
                _ = owner.WaitForExit((int)OpenTimeout.TotalMilliseconds);
            }
            catch (ArgumentException)
            {
                // The program already ended
            }
            catch (ElementNotAvailableException)
            {
                // Already closed
            }
        }
    }
}
