using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.History;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Security;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Scenarios;

/// <summary>
/// Plays every scenario of the test bench (terrain-essai/scenarios.json) the way the user will by hand, so that
/// what the page announces is what the application does.
/// </summary>
public sealed class TestBenchScenarioTests
{
    private static readonly Lazy<List<BenchScenario>> AllScenarios = new(
        () => BenchScenario.Load(Path.Combine(AppContext.BaseDirectory, "Scenarios", "scenarios.json")));

    public static TheoryData<string> AutomaticScenarioIds()
    {
        return Ids(scenario => !scenario.Manual);
    }

    public static TheoryData<string> FormScenarioIds()
    {
        return Ids(scenario => !scenario.Manual && scenario.Caret is null && scenario.LearnFrom.Count == 0);
    }

    [Fact]
    public void Bench_HasUniqueScenarioAndFieldIds()
    {
        var scenarios = AllScenarios.Value;

        Assert.Equal(scenarios.Count, scenarios.Select(scenario => scenario.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(scenarios, scenario => Assert.Equal(
            scenario.Form.Count,
            scenario.Form.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count()));
        Assert.All(scenarios, scenario => Assert.True(
            scenario.Form.All(field => scenario.Expected.ContainsKey(field.Id)),
            $"Le scénario {scenario.Id} doit dire ce qu'il attend de chaque champ."));
        Assert.All(scenarios.SelectMany(scenario => scenario.LearnFrom), id => Assert.Contains(scenarios, scenario => scenario.Id == id));
    }

    [Theory]
    [MemberData(nameof(AutomaticScenarioIds))]
    public void Scenario_FirstPress_GivesTheAnnouncedValues(string id)
    {
        var scenario = Find(id);
        using var history = new FillHistory(new FakeHistoryStore());
        foreach (var learnt in scenario.LearnFrom)
        {
            using var lesson = new Bench(Find(learnt), history);
            lesson.Press();
        }

        using var bench = new Bench(scenario, history);

        bench.Press();

        AssertValues(scenario, scenario.Expected, bench.Fields.CurrentValues);
        AssertSuggestions(scenario, bench.Notifier.Reports.Count > 0 ? bench.Notifier.Reports[^1] : null);
    }

    [Theory]
    [MemberData(nameof(FormScenarioIds))]
    public void Scenario_SecondPress_GivesTheAnnouncedValues(string id)
    {
        var scenario = Find(id);
        using var history = new FillHistory(new FakeHistoryStore());
        using var bench = new Bench(scenario, history);

        bench.Press();
        bench.Clock.Now += TimeSpan.FromSeconds(2);
        bench.Press();

        // Without a second press announced, pressing again changes nothing the first press did not
        AssertValues(scenario, scenario.SecondPress ?? scenario.Expected, bench.Fields.CurrentValues);
    }

    private static TheoryData<string> Ids(Func<BenchScenario, bool> keep)
    {
        var data = new TheoryData<string>();
        foreach (var scenario in AllScenarios.Value.Where(keep))
        {
            data.Add(scenario.Id);
        }

        return data;
    }

    private static BenchScenario Find(string id)
    {
        return AllScenarios.Value.Single(item => item.Id == id);
    }

    private static void AssertValues(BenchScenario scenario, Dictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        foreach (var field in scenario.Form)
        {
            var value = actual.GetValueOrDefault(field.Id, string.Empty);
            Assert.True(
                string.Equals(expected[field.Id], value, StringComparison.Ordinal),
                $"Scénario {scenario.Id}, champ « {field.Label} » : attendu « {expected[field.Id]} », obtenu « {value} ».");
        }
    }

    private static void AssertSuggestions(BenchScenario scenario, FillReport? report)
    {
        foreach (var (fieldId, values) in scenario.Suggested)
        {
            var proposed = report?.FieldsToCheck.FirstOrDefault(toCheck => toCheck.Field.Id == fieldId)?.Suggestions
                .Select(suggestion => suggestion.Value)
                .ToList() ?? new List<string>();
            Assert.True(
                values.All(proposed.Contains),
                $"Scénario {scenario.Id}, champ {fieldId} : propositions attendues « {string.Join(" / ", values)} », obtenues « {string.Join(" / ", proposed)} ».");
        }
    }

    /// <summary>
    /// The screen of a scenario: the page of the form in front, the sources behind it, and the cursor where the
    /// scenario puts it.
    /// </summary>
    private sealed class Bench : IDisposable
    {
        private readonly FakeHotkeyListener _listener = new();
        private readonly ShortcutActivationService _service;
        private readonly ProfileBook _profile;
        private readonly SensitiveGate _gate = new(new FakePresenceCheck());

        public Bench(BenchScenario scenario, FillHistory history)
        {
            foreach (var field in scenario.Form.Where(field => !string.IsNullOrEmpty(field.Value)))
            {
                Fields.SimulateUserTyping(field.Id, field.Value!);
            }

            var memory = new FillMemory();
            history.Observe(memory);
            var page = new WindowText
            {
                Title = scenario.Title,
                Rank = 0,
                Text = string.Join('\n', new[] { scenario.Title, scenario.Page ?? string.Empty }.Concat(scenario.Form.Select(field => field.Label))),
                Address = scenario.Address ?? string.Empty,
            };
            var sources = scenario.Sources.Select((source, index) => new WindowText
            {
                Title = source.Title,
                Rank = index + 1,
                Text = source.Text,
                IsReadFromImage = source.Image,
                Fields = (source.Fields ?? new List<BenchField>())
                    .Where(field => field.Kind != "password" && !string.IsNullOrEmpty(field.Value))
                    .Select(field => new FieldText { Label = field.Label, Value = field.Value! })
                    .ToList(),
                Links = (source.Links ?? new List<BenchLink>())
                    .Select(link => new LinkText { Text = link.Text, Address = link.Href })
                    .ToList(),
            });
            var reader = new FakeWindowTextReader { Windows = sources.Prepend(page).ToList() };
            var profileStore = new FakeProfileStore();
            profileStore.Preload(scenario.Profile.Select(value => value.ToProfileValue()).ToArray());
            _profile = new ProfileBook(profileStore);
            _service = new ShortcutActivationService(
                _listener,
                new FakeFormReader { Fields = scenario.Form.Select(ToFormField).ToList() },
                Fields,
                new ICandidateSource[] { new ScreenCandidateSource(reader), new ProfileCandidateSource(_profile), history },
                Notifier,
                memory,
                new FakeFieldHighlighter())
            {
                TimeProvider = Clock,
                SensitiveGate = _gate,
            };

            if (scenario.Caret is { } caretId)
            {
                var field = scenario.Form.Single(item => item.Id == caretId);
                var caret = new BenchCaret(Fields, field, scenario.Pick);
                _service.CaretReader = caret;
                _service.Picker = caret;
                _service.Inserter = caret;
            }

            _ = _service.Start(Hotkey.Default);
        }

        public FakeFieldWriter Fields { get; } = new();

        public RecordingNotifier Notifier { get; } = new();

        public FakeTimeProvider Clock { get; } = new();

        public void Press()
        {
            _listener.SimulatePress();
        }

        public void Dispose()
        {
            _service.Dispose();
            _listener.Dispose();
            _profile.Dispose();
            _gate.Dispose();
        }

        private static FormField ToFormField(BenchField field)
        {
            return new FormField
            {
                Id = field.Id,
                Label = field.Label,
                Hint = field.Hint ?? string.Empty,
                Kind = field.Kind switch
                {
                    "password" => FieldKind.Password,
                    "choice" or "radio" => FieldKind.Choice,
                    "checkbox" => FieldKind.CheckBox,
                    _ => FieldKind.Text,
                },
                HasValue = !string.IsNullOrEmpty(field.Value),
                Options = field.Options ?? new List<string>(),
            };
        }
    }

    /// <summary>
    /// The cursor at the end of a field: what is written goes after its text, and the list is played as the scenario says.
    /// </summary>
    private sealed class BenchCaret(FakeFieldWriter fields, BenchField field, BenchPick? pick) : ICaretReader, IPastePicker, ITextInserter
    {
        public Task<CaretContext?> ReadCaretAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<CaretContext?>(new CaretContext
            {
                IsEditable = true,
                FocusedFieldId = field.Id,
                FieldLabel = field.Label,
                TextBefore = fields.CurrentValues.GetValueOrDefault(field.Id, string.Empty),
            });
        }

        public Task<string?> PickAsync(PickRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(BenchPick.Play(request, pick));
        }

        public Task<bool> InsertAsync(string text, CancellationToken cancellationToken)
        {
            fields.SimulateUserTyping(field.Id, fields.CurrentValues.GetValueOrDefault(field.Id, string.Empty) + text);
            return Task.FromResult(true);
        }
    }
}
