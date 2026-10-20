namespace PasteFlow.Core.Tests.Scenarios;

internal sealed record BenchLink
{
    public required string Text { get; init; }

    public required string Href { get; init; }
}
