namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Clock whose time the tests set.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}
