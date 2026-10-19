using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Security;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Activation;

public sealed class SensitiveFillTests : IDisposable
{
    private const string Iban = "FR76 3000 6000 0112 3456 7890 189";

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeFieldWriter _fieldWriter = new();
    private readonly FakeFieldHighlighter _highlighter = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakePresenceCheck _presence = new();
    private readonly SensitiveGate _gate;
    private readonly ShortcutActivationService _service;

    public SensitiveFillTests()
    {
        _gate = new SensitiveGate(_presence);
        var formReader = new FakeFormReader { Fields = new[] { TestFields.Text("iban", "Votre IBAN"), TestFields.Text("email", "Votre e-mail") } };
        var source = new FakeCandidateSource
        {
            Candidates = new[]
            {
                new Candidate { Value = Iban, Kind = CandidateKind.Iban, Label = "IBAN", SourceName = "Ma fiche", SourceRank = 0, IsFromProfile = true, IsSensitive = true },
                new Candidate { Value = "antoine.b@orange.fr", Kind = CandidateKind.Email, Label = "E-mail", SourceName = "Ma fiche", SourceRank = 0, IsFromProfile = true },
            },
        };
        _service = new ShortcutActivationService(_listener, formReader, _fieldWriter, new[] { source }, _notifier, new FillMemory(), _highlighter)
        {
            SensitiveGate = _gate,
        };
        _ = _service.Start(Hotkey.Default);
    }

    [Fact]
    public void SensitiveValue_WithWindowsHello_IsWritten()
    {
        _listener.SimulatePress();

        Assert.Equal(1, _presence.Count);
        Assert.Equal(Iban, _fieldWriter.WrittenValues["iban"]);
        Assert.Equal("antoine.b@orange.fr", _fieldWriter.WrittenValues["email"]);
    }

    [Fact]
    public void SensitiveValue_WithinThirtyMinutes_IsWrittenWithoutAskingAgain()
    {
        _listener.SimulatePress();

        _listener.SimulatePress();

        Assert.Equal(1, _presence.Count);
    }

    [Theory]
    [InlineData(PresenceResult.Refused)]
    [InlineData(PresenceResult.Unavailable)]
    public void SensitiveValue_WithoutWindowsHello_IsNeitherWrittenNorShown(PresenceResult answer)
    {
        _presence.Answer = answer;

        _listener.SimulatePress();

        Assert.False(_fieldWriter.WrittenValues.ContainsKey("iban"));
        Assert.Equal("antoine.b@orange.fr", _fieldWriter.WrittenValues["email"]);
        Assert.Contains("Infos sensibles non remplies", Assert.Single(_notifier.Reports).Notes.Single(), StringComparison.Ordinal);
        Assert.DoesNotContain(_highlighter.HighlightedFields.SelectMany(field => field.Suggestions), suggestion => suggestion.Value.Contains("3000", StringComparison.Ordinal));
    }

    [Fact]
    public void SensitiveValue_WithoutGate_IsNeverUsed()
    {
        _service.SensitiveGate = null;

        _listener.SimulatePress();

        Assert.False(_fieldWriter.WrittenValues.ContainsKey("iban"));
        Assert.Equal(0, _presence.Count);
    }

    public void Dispose()
    {
        _service.Dispose();
        _gate.Dispose();
        _listener.Dispose();
    }
}
