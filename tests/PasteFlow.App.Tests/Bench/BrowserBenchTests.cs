using System.IO;
using System.Text.Json;
using PasteFlow.App.Forms;
using PasteFlow.App.Screen;
using PasteFlow.App.Tests.Fakes;
using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Notifications;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Undo;

namespace PasteFlow.App.Tests.Bench;

/// <summary>
/// The automatic tester: plays every automatic scenario of the test bench in a real browser, the source page in one
/// window and the form in another, with the real reading of the screen and the real writing in the fields.
/// </summary>
/// <remarks>
/// Only the choice of the windows is simplified: the form is read in its window and the source in the window just
/// behind, whatever the order of the windows on the desktop of the test machine.
/// </remarks>
public sealed class BrowserBenchTests
{
    private static readonly TimeSpan FillTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan SourceTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ValueSettleTimeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<List<Scenario>> Scenarios = new(Load);

    public static TheoryData<string> AutomaticScenarioIds()
    {
        var data = new TheoryData<string>();
        foreach (var scenario in Scenarios.Value.Where(scenario => !scenario.Manual))
        {
            data.Add(scenario.Id);
        }

        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(AutomaticScenarioIds))]
    public async Task Scenario_InARealBrowser_GivesTheAnnouncedValues(string id)
    {
        Skip.If(BrowserWindow.BrowserPath is null, "Aucun navigateur Edge ou Chrome sur cette machine.");
        var scenario = Scenarios.Value.Single(item => item.Id == id);
        var formLabels = scenario.Form.Select(field => field.Label).ToList();

        using var source = BrowserWindow.Open(PageUrl(id, "source"), scenario.Sources[0].Title);
        using var form = BrowserWindow.Open(PageUrl(id, "form"), formLabels[0]);
        var reader = new SourceBehindForm(form.Handle, source.Handle);
        await WaitForSourceAsync(reader, scenario);

        var formAccess = new UiAutomationFormAccess();
        using var notifier = new WaitingNotifier();
        using var listener = new FakeHotkeyListener();
        using var service = new ShortcutActivationService(
            listener,
            new FormOfWindow(formAccess, form.Handle),
            formAccess,
            new ICandidateSource[] { new ScreenCandidateSource(reader) },
            notifier,
            new FillMemory(),
            new NoHighlight());
        _ = service.Start(Hotkey.Default);

        var screen = Describe(await reader.ReadWindowsAsync(CancellationToken.None));
        listener.SimulatePress();
        await notifier.WaitForFillAsync(1);
        AssertValues(scenario, scenario.Expected, form, $"{Decisions(notifier.Reports[0])}\n{screen}");

        if (scenario.SecondPress is { } secondPress)
        {
            var fieldsRead = await formAccess.ReadFormAsync(form.Handle, CancellationToken.None);
            listener.SimulatePress();
            await notifier.WaitForFillAsync(2);
            var fieldList = string.Join('\n', fieldsRead.Select(field => $"[champ lu] {field.Kind} « {field.Label} » ({field.TechnicalName}) rempli={field.HasValue}"));
            AssertValues(scenario, secondPress, form, $"{Decisions(notifier.Reports[1])}\n{fieldList}\n{screen}");
        }
    }

    private static Uri PageUrl(string id, string mode)
    {
        var page = Path.Combine(AppContext.BaseDirectory, "Bench", "index.html");
        return new Uri($"{new Uri(page).AbsoluteUri}?mode={mode}#{id}");
    }

    private static async Task WaitForSourceAsync(SourceBehindForm reader, Scenario scenario)
    {
        // The first reading of a browser page through UI Automation is slow: the tester waits until it gives the source
        var expected = scenario.Sources[0];
        var deadline = DateTime.UtcNow + SourceTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var windows = await reader.ReadWindowsAsync(CancellationToken.None);
            var behind = windows.FirstOrDefault(window => window.Rank == 1);
            var isReady = behind is not null && (expected.Image
                || (expected.Fields?.Count > 0 ? behind.Fields.Count > 0 : behind.Text.Contains(FirstLine(expected.Text), StringComparison.Ordinal)));
            if (isReady)
            {
                return;
            }

            await Task.Delay(300);
        }

        throw new TimeoutException($"La source du scénario {scenario.Id} n'a pas pu être lue.");
    }

    private static string FirstLine(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
    }

    // Shows what the fill decided for each field, to understand a failure
    private static string Decisions(FillReport report)
    {
        var lines = new List<string> { $"Bilan : {report.Summary()}" };
        lines.AddRange(report.FilledValues.Select(filled => $"[écrit] {filled.FieldLabel} = {filled.Value} ({filled.SourceName})"));
        lines.AddRange(report.FieldsToCheck.Select(toCheck =>
            $"[à vérifier] {toCheck.Field.Label} : {string.Join(" / ", toCheck.Suggestions.Select(suggestion => suggestion.Value))}"));
        lines.AddRange(report.FailedFields.Select(field => $"[refusé] {field.Label}"));
        return string.Join('\n', lines);
    }

    // Shows what was read on screen, to understand a failure: special characters made visible
    private static string Describe(IReadOnlyList<WindowText> windows)
    {
        static string Visible(string text) => text.Replace("\uFFFC", "[OBJ]", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal);

        return string.Join(
            "\n",
            windows.Select(window => $"--- Fenêtre {window.Rank} « {window.Title} » :\n{Visible(window.Text)}"
                + string.Concat(window.Fields.Select(field => $"\n[champ] {field.Label} = {Visible(field.Value)}"))));
    }

    private static void AssertValues(Scenario scenario, Dictionary<string, string> expected, BrowserWindow form, string screen)
    {
        // A browser shows a value written through UI Automation a moment later: the values are read again for a while
        var deadline = DateTime.UtcNow + ValueSettleTimeout;
        var mismatches = Mismatches(scenario, expected, form);
        while (mismatches.Count > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(200);
            mismatches = Mismatches(scenario, expected, form);
        }

        Assert.True(mismatches.Count == 0, $"Scénario {scenario.Id} dans le navigateur :\n{string.Join('\n', mismatches)}\nÉcran lu :\n{screen}");
    }

    private static List<string> Mismatches(Scenario scenario, Dictionary<string, string> expected, BrowserWindow form)
    {
        return scenario.Form
            .Where(field => field.Kind is not ("password" or "choice"))
            .Select(field => (field.Label, Expected: expected[field.Id].ReplaceLineEndings("\n"), Actual: form.ValueOf(field.Label).TrimEnd()))
            .Where(result => !string.Equals(result.Expected, result.Actual, StringComparison.Ordinal))
            .Select(result => $"« {result.Label} » : attendu « {result.Expected} », obtenu « {result.Actual} »")
            .ToList();
    }

    private static List<Scenario> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Bench", "scenarios.json");
        return JsonSerializer.Deserialize<BenchFile>(File.ReadAllText(path), JsonOptions)?.Scenarios ?? new List<Scenario>();
    }

    /// <summary>
    /// Reads the form of the given window, whichever window is in front on the test machine.
    /// </summary>
    private sealed class FormOfWindow(UiAutomationFormAccess formAccess, IntPtr window) : IFormReader
    {
        public Task<IReadOnlyList<FormField>> ReadActiveFormAsync(CancellationToken cancellationToken)
        {
            return formAccess.ReadFormAsync(window, cancellationToken);
        }
    }

    /// <summary>
    /// Reads the form window as the active one and the source window as the one just behind it.
    /// </summary>
    private sealed class SourceBehindForm(IntPtr form, IntPtr source) : IWindowTextReader
    {
        public Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
        {
            return Task.Run<IReadOnlyList<WindowText>>(
                () => new[] { UiAutomationWindowTextReader.ReadWindow(form, 0), UiAutomationWindowTextReader.ReadWindow(source, 1) }
                    .OfType<WindowText>()
                    .ToList(),
                cancellationToken);
        }
    }

    private sealed class WaitingNotifier : IUserNotifier, IDisposable
    {
        private readonly SemaphoreSlim _fills = new(0);
        private int _fillCount;

        public List<FillReport> Reports { get; } = new();

        public void Notify(string message)
        {
            // Only the end of a fill matters to the tester
        }

        public void NotifyFill(FillReport report)
        {
            lock (Reports)
            {
                Reports.Add(report);
            }

            _ = Interlocked.Increment(ref _fillCount);
            _ = _fills.Release();
        }

        public void Dispose()
        {
            _fills.Dispose();
        }

        public async Task WaitForFillAsync(int count)
        {
            while (Volatile.Read(ref _fillCount) < count)
            {
                if (!await _fills.WaitAsync(FillTimeout))
                {
                    throw new TimeoutException("Le remplissage ne s'est pas terminé.");
                }
            }
        }
    }

    private sealed class NoHighlight : IFieldHighlighter
    {
        public void Highlight(IReadOnlyList<FieldToCheck> fields)
        {
            // The frames are not part of what the tester checks
        }
    }

    private sealed record BenchFile(List<Scenario> Scenarios);

    private sealed record Scenario
    {
        public required string Id { get; init; }

        public bool Manual { get; init; }

        public List<Source> Sources { get; init; } = new();

        public List<Field> Form { get; init; } = new();

        public Dictionary<string, string> Expected { get; init; } = new(StringComparer.Ordinal);

        public Dictionary<string, string>? SecondPress { get; init; }
    }

    private sealed record Source
    {
        public required string Title { get; init; }

        public string Text { get; init; } = string.Empty;

        public bool Image { get; init; }

        public List<Field>? Fields { get; init; }
    }

    private sealed record Field
    {
        public required string Id { get; init; }

        public required string Label { get; init; }

        public string? Kind { get; init; }
    }
}
