using System.Text.Json;

namespace PasteFlow.Core.Tests.Scenarios;

/// <summary>
/// A scenario of the test bench (terrain-essai/scenarios.json), as the tests read it.
/// </summary>
internal sealed record BenchScenario
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public required string Id { get; init; }

    public required string Title { get; init; }

    public bool Manual { get; init; }

    public string? Page { get; init; }

    /// <summary>
    /// Gets the field holding the cursor, at the end of its text, when the shortcut is pressed outside a form.
    /// </summary>
    public string? Caret { get; init; }

    /// <summary>
    /// Gets what the user does in the list shown at the cursor.
    /// </summary>
    public BenchPick? Pick { get; init; }

    /// <summary>
    /// Gets the scenarios filled before this one, so that the application learns from them.
    /// </summary>
    public List<string> LearnFrom { get; init; } = new();

    public string? Address { get; init; }

    public List<BenchProfileValue> Profile { get; init; } = new();

    public List<BenchSource> Sources { get; init; } = new();

    public List<BenchField> Form { get; init; } = new();

    public Dictionary<string, string> Expected { get; init; } = new(StringComparer.Ordinal);

    public Dictionary<string, string>? SecondPress { get; init; }

    /// <summary>
    /// Gets the values proposed, not written, for some fields.
    /// </summary>
    public Dictionary<string, List<string>> Suggested { get; init; } = new(StringComparer.Ordinal);

    public static List<BenchScenario> Load(string path)
    {
        return JsonSerializer.Deserialize<BenchFile>(File.ReadAllText(path), JsonOptions)?.Scenarios ?? new List<BenchScenario>();
    }

    private sealed record BenchFile(List<BenchScenario> Scenarios);
}
