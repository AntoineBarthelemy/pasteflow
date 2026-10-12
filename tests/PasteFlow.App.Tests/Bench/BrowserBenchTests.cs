using System.IO;
using System.Text.Json;
using PasteFlow.App.Forms;
using PasteFlow.App.Screen;
using PasteFlow.App.Tests.Fakes;
using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.History;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Notifications;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Security;
using PasteFlow.Core.Undo;

namespace PasteFlow.App.Tests.Bench;

/// <summary>
/// The automatic tester: plays every automatic scenario of the test bench in a real browser, the source page in one
/// window and the form in another, with the real reading of the screen and the real writing in the fields.
/// </summary>
/// <remarks>
/// <para>Only the choice of the windows is simplified: the form is read in its window and the source in the window
/// just behind, whatever the order of the windows on the desktop of the test machine. A scanned source is the picture
/// itself, opened in the browser and read by character recognition.</para>
/// <para>For a paste at the cursor, the list is played as the scenario says and the text chosen is written at the end
/// of the field: the test machine has no screen with a keyboard focus to type in, the typing itself is covered by the
/// unit tests.</para>
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
        var scenario = Find(id);
        var ocr = OcrTextRecognizer.TryCreate();
        Skip.If(scenario.Sources.Exists(source => source.Image) && ocr is null, "Windows ne sait pas lire le texte des images sur cette machine.");

        // What the history learns from the scenarios played before is what it gives back
        using var history = new FillHistory(new InMemoryHistoryStore());
        foreach (var learnt in scenario.LearnFrom)
        {
            await PlayAsync(Find(learnt), history, ocr);
        }

        await PlayAsync(scenario, history, ocr);
    }

    private static Scenario Find(string id)
    {
        return Scenarios.Value.Single(item => item.Id == id);
    }

    private static async Task PlayAsync(Scenario scenario, FillHistory history, OcrTextRecognizer? ocr)
    {
        using var source = OpenSource(scenario);
        using var form = BrowserWindow.Open(PageUrl(scenario.Id, "form"), scenario.Form[0].Label);
        var isImage = scenario.Sources.Exists(item => item.Image);
        var reader = new BenchWindows(form.Handle, source?.Handle, isImage ? new UiAutomationWindowTextReader(ocr) : null, scenario.Address);
        await WaitForSourceAsync(reader, scenario);

        var formAccess = new UiAutomationFormAccess();
        var memory = new FillMemory();
        history.Observe(memory);
        using var notifier = new WaitingNotifier();
        using var listener = new FakeHotkeyListener();
        using var profile = new ProfileBook(new InMemoryProfileStore(scenario.Profile.Select(value => value.ToProfileValue()).ToArray()));
        using var gate = new SensitiveGate(new FakePresenceCheck());
        using var service = new ShortcutActivationService(
            listener,
            new FormOfWindow(formAccess, form.Handle),
            formAccess,
            new ICandidateSource[] { new ScreenCandidateSource(reader), new ProfileCandidateSource(profile), history },
            notifier,
            memory,
            new NoHighlight())
        {
            SensitiveGate = gate,
        };
        var caret = scenario.Caret is { } caretId
            ? new BrowserCaret(formAccess, form, scenario.Form.Single(field => field.Id == caretId).Label, scenario.Pick)
            : null;
        if (caret is not null)
        {
            service.CaretReader = caret;
            service.Picker = caret;
            service.Inserter = caret;
        }

        _ = service.Start(Hotkey.Default);

        var screen = Describe(await reader.ReadWindowsAsync(CancellationToken.None));
        listener.SimulatePress();
        if (caret is not null)
        {
            await caret.WaitForPasteAsync(screen);
            AssertValues(scenario, scenario.Expected, form, screen);
            return;
        }

        await notifier.WaitForFillAsync(1);
        AssertValues(scenario, scenario.Expected, form, $"{Decisions(notifier.Reports[0])}\n{screen}");
        AssertSuggestions(scenario, notifier.Reports[0], screen);

        if (scenario.SecondPress is { } secondPress)
        {
            var fieldsRead = await formAccess.ReadFormAsync(form.Handle, CancellationToken.None);
            listener.SimulatePress();
            await notifier.WaitForFillAsync(2);
            var fieldList = string.Join('\n', fieldsRead.Select(field => $"[champ lu] {field.Kind} « {field.Label} » ({field.TechnicalName}) rempli={field.HasValue}"));
            AssertValues(scenario, secondPress, form, $"{Decisions(notifier.Reports[1])}\n{fieldList}\n{screen}");
        }
    }

    private static BrowserWindow? OpenSource(Scenario scenario)
    {
        if (scenario.Sources.Count == 0)
        {
            return null;
        }

        if (scenario.Sources[0].Image)
        {
            // The scan itself: a page of text around it would be read instead of the picture
            return BrowserWindow.Open(new Uri(Path.Combine(AppContext.BaseDirectory, "Bench", "bon-de-commande.png")), null);
        }

        return BrowserWindow.Open(PageUrl(scenario.Id, "source"), scenario.Sources[0].Title);
    }

    private static Uri PageUrl(string id, string mode)
    {
        var page = Path.Combine(AppContext.BaseDirectory, "Bench", "index.html");
        return new Uri($"{new Uri(page).AbsoluteUri}?mode={mode}#{id}");
    }

    private static async Task WaitForSourceAsync(BenchWindows reader, Scenario scenario)
    {
        if (scenario.Sources.Count == 0)
        {
            return;
        }

        // The first reading of a browser page through UI Automation is slow: the tester waits until it gives the source
        var expected = scenario.Sources[0];
        var deadline = DateTime.UtcNow + SourceTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var windows = await reader.ReadWindowsAsync(CancellationToken.None);
            var behind = windows.FirstOrDefault(window => window.Rank == 1);
            var isReady = behind is not null && (expected.Image
                ? behind.IsReadFromImage && behind.Text.Contains(FirstWord(expected.Text), StringComparison.OrdinalIgnoreCase)
                : (expected.Fields?.Count > 0 ? behind.Fields.Count > 0 : behind.Text.Contains(FirstLine(expected.Text), StringComparison.Ordinal)));
            if (isReady)
            {
                return;
            }

            await Task.Delay(300);
        }

        throw new TimeoutException($"La source du scénario {scenario.Id} n'a pas pu être lue.");
    }

    private static string FirstWord(string text)
    {
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
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

    private static void AssertSuggestions(Scenario scenario, FillReport report, string screen)
    {
        foreach (var (fieldId, values) in scenario.Suggested)
        {
            var label = scenario.Form.Single(field => field.Id == fieldId).Label;
            var proposed = report.FieldsToCheck.FirstOrDefault(toCheck => toCheck.Field.Label == label)?.Suggestions
                .Select(suggestion => suggestion.Value)
                .ToList() ?? new List<string>();
            Assert.True(
                values.All(proposed.Contains),
                $"Scénario {scenario.Id}, « {label} » : propositions attendues « {string.Join(" / ", values)} », obtenues « {string.Join(" / ", proposed)} ».\n{Decisions(report)}\nÉcran lu :\n{screen}");
        }
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
    /// Reads the form window as the active one and the source window, if any, as the one just behind it.
    /// </summary>
    /// <param name="form">The window of the form.</param>
    /// <param name="source">The window of the source, if the scenario has one.</param>
    /// <param name="imageReader">The reader that turns to the picture of the source when it has too little text.</param>
    /// <param name="address">The address the scenario pretends the page has, since the bench is a local file.</param>
    private sealed class BenchWindows(IntPtr form, IntPtr? source, UiAutomationWindowTextReader? imageReader, string? address) : IWindowTextReader
    {
        public async Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
        {
            var windows = new List<WindowText>();
            if (await Task.Run(() => UiAutomationWindowTextReader.ReadWindow(form, 0), cancellationToken) is { } formText)
            {
                windows.Add(address is null ? formText : formText with { Address = address });
            }

            if (source is { } handle)
            {
                var sourceText = imageReader is null
                    ? await Task.Run(() => UiAutomationWindowTextReader.ReadWindow(handle, 1), cancellationToken)
                    : await imageReader.ReadWithFallbackAsync(handle, 1, cancellationToken);
                if (sourceText is not null)
                {
                    windows.Add(sourceText);
                }
            }

            return windows;
        }
    }

    /// <summary>
    /// The cursor at the end of a field of the page: the list is played as the scenario says, and the text chosen is
    /// written after the text of the field.
    /// </summary>
    /// <param name="formAccess">The real reading of the fields.</param>
    /// <param name="form">The window of the form.</param>
    /// <param name="label">The label of the field holding the cursor.</param>
    /// <param name="pick">What the user does in the list.</param>
    private sealed class BrowserCaret(UiAutomationFormAccess formAccess, BrowserWindow form, string label, Pick? pick)
        : ICaretReader, IPastePicker, ITextInserter
    {
        private readonly TaskCompletionSource _pasted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CaretContext?> ReadCaretAsync(CancellationToken cancellationToken)
        {
            // The real description of the field, the text before the cursor being all its text
            return Task.Run<CaretContext?>(() => formAccess.DescribeCaret(form.Field(label)) with { TextBefore = form.ValueOf(label) }, cancellationToken);
        }

        public Task<string?> PickAsync(PickRequest request, CancellationToken cancellationToken)
        {
            var chosen = Play(new PickerState(request), pick);
            if (chosen is null)
            {
                _ = _pasted.TrySetException(new InvalidOperationException(
                    $"Rien choisi dans la liste : {string.Join(" / ", new PickerState(request).Items.Select(item => item.Text))}"));
            }

            return Task.FromResult(chosen);
        }

        public Task<bool> InsertAsync(string text, CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    var isWritten = form.AppendTo(label, text);
                    _ = _pasted.TrySetResult();
                    return isWritten;
                },
                cancellationToken);
        }

        public async Task WaitForPasteAsync(string screen)
        {
            if (await Task.WhenAny(_pasted.Task, Task.Delay(FillTimeout)) != _pasted.Task)
            {
                throw new TimeoutException($"Rien n'a été collé au curseur.\nÉcran lu :\n{screen}");
            }

            await _pasted.Task;
        }

        private static string? Play(PickerState state, Pick? pick)
        {
            if (pick is null)
            {
                // The scenario expects a sure value: the list should not even open
                return null;
            }

            if (pick.Query is not null)
            {
                state.SetQuery(pick.Query);
            }

            for (var count = 0; count < pick.Widen; count++)
            {
                state.NextCut();
            }

            for (var count = 0; count < pick.ShrinkEnd; count++)
            {
                state.ShrinkEnd();
            }

            return pick.Choose is null ? state.Selected?.Text : state.Items.FirstOrDefault(item => item.Text == pick.Choose)?.Text;
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

        public string? Caret { get; init; }

        public Pick? Pick { get; init; }

        public List<string> LearnFrom { get; init; } = new();

        public string? Address { get; init; }

        public List<ProfileItem> Profile { get; init; } = new();

        public Dictionary<string, List<string>> Suggested { get; init; } = new(StringComparer.Ordinal);

        public List<Source> Sources { get; init; } = new();

        public List<Field> Form { get; init; } = new();

        public Dictionary<string, string> Expected { get; init; } = new(StringComparer.Ordinal);

        public Dictionary<string, string>? SecondPress { get; init; }
    }

    private sealed record ProfileItem
    {
        public required ProfileTopic Topic { get; init; }

        public required string Value { get; init; }

        public int Uses { get; init; }

        public string Sites { get; init; } = string.Empty;

        public ProfileValue ToProfileValue()
        {
            return new ProfileValue { Topic = Topic, Value = Value, UseCount = Uses, Sites = Sites };
        }
    }

    private sealed record Pick
    {
        public string? Query { get; init; }

        public int Widen { get; init; }

        public int ShrinkEnd { get; init; }

        public string? Choose { get; init; }
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
