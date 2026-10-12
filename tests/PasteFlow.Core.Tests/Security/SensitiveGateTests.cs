using PasteFlow.Core.Security;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Security;

public sealed class SensitiveGateTests : IDisposable
{
    private readonly FakePresenceCheck _check = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly SensitiveGate _gate;

    public SensitiveGateTests()
    {
        _gate = new SensitiveGate(_check, _clock);
    }

    [Fact]
    public void Gate_IsClosedAtFirst()
    {
        Assert.False(_gate.IsOpen);
    }

    [Fact]
    public async Task OpenAsync_AsksOnceThenStaysOpenThirtyMinutes()
    {
        Assert.Equal(PresenceResult.Verified, await _gate.OpenAsync("Afficher ton IBAN", CancellationToken.None));
        _clock.Now += TimeSpan.FromMinutes(29);
        Assert.Equal(PresenceResult.Verified, await _gate.OpenAsync("Remplir ton IBAN", CancellationToken.None));

        Assert.Equal(1, _check.Count);
        Assert.Equal("Afficher ton IBAN", _check.LastReason);
        Assert.True(_gate.IsOpen);
    }

    [Fact]
    public async Task OpenAsync_AfterThirtyMinutes_AsksAgain()
    {
        _ = await _gate.OpenAsync("Afficher ton IBAN", CancellationToken.None);
        _clock.Now += SensitiveGate.OpenDuration;

        Assert.False(_gate.IsOpen);
        _ = await _gate.OpenAsync("Afficher ton IBAN", CancellationToken.None);
        Assert.Equal(2, _check.Count);
    }

    [Fact]
    public async Task Close_AsksAgainAtTheNextRequest()
    {
        _ = await _gate.OpenAsync("Afficher ton IBAN", CancellationToken.None);

        _gate.Close();

        Assert.False(_gate.IsOpen);
        _ = await _gate.OpenAsync("Afficher ton IBAN", CancellationToken.None);
        Assert.Equal(2, _check.Count);
    }

    [Theory]
    [InlineData(PresenceResult.Refused)]
    [InlineData(PresenceResult.Unavailable)]
    public async Task OpenAsync_NotVerified_StaysClosed(PresenceResult answer)
    {
        _check.Answer = answer;

        Assert.Equal(answer, await _gate.OpenAsync("Afficher ton IBAN", CancellationToken.None));
        Assert.False(_gate.IsOpen);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
