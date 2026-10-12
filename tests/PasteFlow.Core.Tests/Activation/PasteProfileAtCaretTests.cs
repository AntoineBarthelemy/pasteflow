using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Security;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Activation;

/// <summary>
/// The values of the profile in a text written alone: ordinary ones pasted or listed, sensitive ones only listed.
/// </summary>
public sealed class PasteProfileAtCaretTests : IDisposable
{
    private const string MyEmail = "antoine.b@orange.fr";
    private const string Iban = "FR76 3000 6000 0112 3456 7890 189";

    private static readonly Candidate[] Profile =
    {
        new() { Value = MyEmail, Kind = CandidateKind.Email, Label = "E-mail", SourceName = "Ma fiche", SourceRank = 0, IsFromProfile = true },
        new() { Value = Iban, Kind = CandidateKind.Iban, Label = "IBAN", SourceName = "Ma fiche", SourceRank = 0, IsFromProfile = true, IsSensitive = true },
    };

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeFormReader _formReader = new() { Fields = new[] { TestFields.Text("message", "Message") } };
    private readonly FakeFieldWriter _fields = new();
    private readonly FakeUserNotifier _notifier = new();
    private readonly FakeCaret _caret = new();
    private readonly FakePresenceCheck _presence = new();
    private readonly SensitiveGate _gate;
    private WindowText[] _windows = { new() { Title = "Claude", Rank = 1, Text = "Léa Martin\nlea.martin@atelier-nova.fr" } };

    public PasteProfileAtCaretTests()
    {
        _gate = new SensitiveGate(_presence);
    }

    [Fact]
    public void WordsAboutTheUser_PasteTheirOwnValueDirectly()
    {
        _caret.Caret = Caret("Bonjour, mon mail : ");

        Press();

        Assert.Equal(new[] { MyEmail }, _caret.InsertedTexts);
        Assert.Equal($"Collé : {MyEmail} (Ma fiche)", Assert.Single(_notifier.Messages));
        Assert.Equal(0, _presence.Count);
    }

    [Fact]
    public void WordsAboutSomeoneElse_PasteTheValueOfTheScreen()
    {
        _caret.Caret = Caret("Tu peux écrire à Léa, son mail : ");

        Press();

        Assert.Equal(new[] { "lea.martin@atelier-nova.fr" }, _caret.InsertedTexts);
    }

    [Fact]
    public void NothingSure_ListsTheOrdinaryValuesOfTheProfileButNeverTheSensitiveOnes()
    {
        _caret.Caret = Caret("Tiens : ");

        Press();

        var request = Assert.IsType<PickRequest>(_caret.ShownRequest);
        Assert.Contains(request.Suggestions, candidate => candidate.Value == MyEmail);
        Assert.DoesNotContain(request.Suggestions, candidate => candidate.IsSensitive);
        Assert.Equal(0, _presence.Count);
    }

    [Fact]
    public void ListSearch_FindsTheValuesOfTheProfile()
    {
        _caret.Caret = Caret("Tiens : ");

        Press();

        var request = Assert.IsType<PickRequest>(_caret.ShownRequest);
        Assert.Contains(request.Finder.Find("orange", PassageCut.Line), passage => passage.Text.Contains(MyEmail, StringComparison.Ordinal));
    }

    [Fact]
    public void WordsAskingForTheIban_ListItAfterWindowsHello_NeverPasteIt()
    {
        _caret.Caret = Caret("Voici mon IBAN : ");

        Press();

        Assert.Empty(_caret.InsertedTexts);
        Assert.Equal(1, _presence.Count);
        var request = Assert.IsType<PickRequest>(_caret.ShownRequest);
        Assert.Contains(request.Suggestions, candidate => candidate.Value == Iban);
    }

    [Fact]
    public void WordsAskingForTheIban_WithoutWindowsHello_LeaveItOut()
    {
        _presence.Answer = PresenceResult.Refused;
        _caret.Caret = Caret("Voici mon IBAN : ");

        Press();

        var request = Assert.IsType<PickRequest>(_caret.ShownRequest);
        Assert.DoesNotContain(request.Suggestions, candidate => candidate.IsSensitive);
        Assert.DoesNotContain(request.Finder.Find("FR76", PassageCut.Line), passage => passage.Text.Contains("3000", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        _gate.Dispose();
        _listener.Dispose();
    }

    private static CaretContext Caret(string textBefore)
    {
        return new CaretContext { IsEditable = true, FocusedFieldId = "message", FieldLabel = "Message", TextBefore = textBefore };
    }

    private void Press()
    {
        var reader = new FakeWindowTextReader { Windows = _windows };
        using var service = new ShortcutActivationService(
            _listener,
            _formReader,
            _fields,
            new ICandidateSource[] { new ScreenCandidateSource(reader), new FakeCandidateSource { Candidates = Profile } },
            _notifier,
            new FillMemory(),
            new FakeFieldHighlighter())
        {
            CaretReader = _caret,
            Picker = _caret,
            Inserter = _caret,
            SensitiveGate = _gate,
        };
        service.Start(Hotkey.Default);

        _listener.SimulatePress();
    }
}
