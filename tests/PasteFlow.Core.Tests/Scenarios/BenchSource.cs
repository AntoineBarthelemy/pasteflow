namespace PasteFlow.Core.Tests.Scenarios;

internal sealed record BenchSource
{
    public required string Title { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool Image { get; init; }

    public List<BenchField>? Fields { get; init; }
}
