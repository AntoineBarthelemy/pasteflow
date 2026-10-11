using System.Text.Json;
using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Scenarios;

/// <summary>
/// Plays every scenario of the test bench (terrain-essai/scenarios.json) the way the user will by hand, so that
/// what the page announces is what the application does.
/// </summary>
public sealed class TestBenchScenarioTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<IReadOnlyList<BenchScenario>> AllScenarios = new(Load);

    public static TheoryData<string> AutomaticScenarioIds()
    {
        var data = new TheoryData<string>();
        foreach (var scenario in AllScenarios.Value.Where(scenario => !scenario.Manual))
        {
            data.Add(scenario.Id);
        }

        return data;
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
    }

    [Theory]
    [MemberData(nameof(AutomaticScenarioIds))]
    public void Scenario_FirstPress_GivesTheAnnouncedValues(string id)
    {
        var scenario = AllScenarios.Value.Single(item => item.Id == id);
        var bench = new Bench(scenario);

        bench.Press();

        AssertValues(scenario, scenario.Expected, bench.Fields.CurrentValues);
    }

    [Theory]
    [MemberData(nameof(AutomaticScenarioIds))]
    public void Scenario_SecondPress_GivesTheAnnouncedValues(string id)
    {
        var scenario = AllScenarios.Value.Single(item => item.Id == id);
        var bench = new Bench(scenario);

        bench.Press();
        bench.Clock.Now += TimeSpan.FromSeconds(2);
        bench.Press();

        // Without a second press announced, pressing again changes nothing the first press did not
        AssertValues(scenario, scenario.SecondPress ?? scenario.Expected, bench.Fields.CurrentValues);
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

    private static List<BenchScenario> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Scenarios", "scenarios.json");
        var bench = JsonSerializer.Deserialize<BenchFile>(File.ReadAllText(path), JsonOptions);
        return bench?.Scenarios ?? new List<BenchScenario>();
    }

    /// <summary>
    /// The screen of a scenario: the page of the form in front, the sources behind it.
    /// </summary>
    private sealed class Bench
    {
        private readonly FakeHotkeyListener _listener = new();
        private readonly ShortcutActivationService _service;

        public Bench(BenchScenario scenario)
        {
            foreach (var field in scenario.Form.Where(field => !string.IsNullOrEmpty(field.Value)))
            {
                Fields.SimulateUserTyping(field.Id, field.Value!);
            }

            var page = new WindowText
            {
                Title = scenario.Title,
                Rank = 0,
                Text = string.Join('\n', new[] { scenario.Title, scenario.Page ?? string.Empty }.Concat(scenario.Form.Select(field => field.Label))),
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
            });
            var reader = new FakeWindowTextReader { Windows = sources.Prepend(page).ToList() };
            _service = new ShortcutActivationService(
                _listener,
                new FakeFormReader { Fields = scenario.Form.Select(ToFormField).ToList() },
                Fields,
                new ICandidateSource[] { new ScreenCandidateSource(reader) },
                new FakeUserNotifier(),
                new FillMemory(),
                new FakeFieldHighlighter())
            {
                TimeProvider = Clock,
            };
            _ = _service.Start(Hotkey.Default);
        }

        public FakeFieldWriter Fields { get; } = new();

        public FakeTimeProvider Clock { get; } = new();

        public void Press()
        {
            _listener.SimulatePress();
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
                    "choice" => FieldKind.Choice,
                    _ => FieldKind.Text,
                },
                HasValue = !string.IsNullOrEmpty(field.Value),
            };
        }
    }

    private sealed record BenchFile(List<BenchScenario> Scenarios);

    private sealed record BenchScenario
    {
        public required string Id { get; init; }

        public required string Title { get; init; }

        public bool Manual { get; init; }

        public string? Page { get; init; }

        public List<BenchSource> Sources { get; init; } = new();

        public List<BenchField> Form { get; init; } = new();

        public Dictionary<string, string> Expected { get; init; } = new(StringComparer.Ordinal);

        public Dictionary<string, string>? SecondPress { get; init; }
    }

    private sealed record BenchSource
    {
        public required string Title { get; init; }

        public string Text { get; init; } = string.Empty;

        public bool Image { get; init; }

        public List<BenchField>? Fields { get; init; }
    }

    private sealed record BenchField
    {
        public required string Id { get; init; }

        public required string Label { get; init; }

        public string? Kind { get; init; }

        public string? Hint { get; init; }

        public string? Value { get; init; }
    }
}
