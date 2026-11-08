namespace PasteFlow.Core.Tests.Scenarios;

internal sealed record BenchField
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public string? Kind { get; init; }

    public string? Hint { get; init; }

    public string? Value { get; init; }

    public List<string>? Options { get; init; }
}
