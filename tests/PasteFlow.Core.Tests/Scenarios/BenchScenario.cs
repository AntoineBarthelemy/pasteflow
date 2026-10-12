using System.Text.Json;
using PasteFlow.Core.Picking;

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

internal sealed record BenchSource
{
    public required string Title { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool Image { get; init; }

    public List<BenchField>? Fields { get; init; }
}

internal sealed record BenchField
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public string? Kind { get; init; }

    public string? Hint { get; init; }

    public string? Value { get; init; }
}

/// <summary>
/// What the user does in the list shown at the cursor: types letters, widens the cut, removes words, then chooses.
/// </summary>
internal sealed record BenchPick
{
    public string? Query { get; init; }

    public int Widen { get; init; }

    public int ShrinkEnd { get; init; }

    /// <summary>
    /// Gets the choice to make by its text; the highlighted one when <see langword="null"/>.
    /// </summary>
    public string? Choose { get; init; }

    /// <summary>
    /// Plays these keys on the list, as the user would.
    /// </summary>
    /// <param name="request">What the list offers.</param>
    /// <param name="pick">What the user does, or <see langword="null"/> when nothing was planned: the list is closed.</param>
    /// <returns>The text chosen, or <see langword="null"/>.</returns>
    public static string? Play(PickRequest request, BenchPick? pick)
    {
        if (pick is null)
        {
            return null;
        }

        var state = new PickerState(request);
        if (pick.Query is not null)
        {
            state.SetQuery(pick.Query);
        }

        for (var step = 0; step < pick.Widen; step++)
        {
            state.NextCut();
        }

        for (var step = 0; step < pick.ShrinkEnd; step++)
        {
            state.ShrinkEnd();
        }

        return pick.Choose is null
            ? state.Selected?.Text
            : state.Items.FirstOrDefault(item => string.Equals(item.Text, pick.Choose, StringComparison.Ordinal))?.Text;
    }
}
