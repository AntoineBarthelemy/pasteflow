using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Undo;

public sealed class UndoShortcutServiceTests : IDisposable
{
    private readonly FakeHotkeyListener _listener = new();
    private readonly FillMemory _fillMemory = new();
    private readonly FakeFieldWriter _fields = new();
    private readonly FakeUserNotifier _notifier = new();

    [Fact]
    public void Start_WhenRegistrationSucceeds_ListensToHotkey()
    {
        using var service = CreateService();

        var isStarted = service.Start(Hotkey.DefaultUndo);

        Assert.True(isStarted);
        Assert.Equal(Hotkey.DefaultUndo, service.ActiveHotkey);
        Assert.Equal(Hotkey.DefaultUndo, _listener.RegisteredHotkey);
    }

    [Fact]
    public void Start_WhenHotkeyIsAlreadyUsed_ReturnsFalse()
    {
        using var listener = new FakeHotkeyListener { AcceptsRegistration = false };
        using var service = new UndoShortcutService(listener, _fillMemory, _fields, _fields, _notifier);

        Assert.False(service.Start(Hotkey.DefaultUndo));
        Assert.Null(service.ActiveHotkey);
    }

    [Fact]
    public void Start_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        using var service = CreateService();
        service.Start(Hotkey.DefaultUndo);

        Assert.Throws<InvalidOperationException>(() => service.Start(Hotkey.DefaultUndo));
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = CreateService();
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Start(Hotkey.DefaultUndo));
    }

    [Fact]
    public void HotkeyPressed_EmptiesFieldsWrittenByLastFill()
    {
        Fill(("email", "antoine.b@orange.fr"), ("city", "Reims"));
        using var service = StartService();

        _listener.SimulatePress();

        Assert.Equal(string.Empty, _fields.CurrentValues["email"]);
        Assert.Equal(string.Empty, _fields.CurrentValues["city"]);
        Assert.Equal("Annulé : 2 champs vidés.", Assert.Single(_notifier.Messages));
    }

    [Fact]
    public void HotkeyPressed_LeavesFieldsChangedByUserSinceTheFill()
    {
        Fill(("email", "antoine.b@orange.fr"), ("city", "Reims"));
        _fields.SimulateUserTyping("city", "Taissy");
        using var service = StartService();

        _listener.SimulatePress();

        Assert.Equal(string.Empty, _fields.CurrentValues["email"]);
        Assert.Equal("Taissy", _fields.CurrentValues["city"]);
        Assert.Equal("Annulé : 1 champ vidé, 1 laissé car modifié depuis.", Assert.Single(_notifier.Messages));
    }

    [Fact]
    public void HotkeyPressed_EmptiesFieldThatOnlyReformattedTheValue()
    {
        Fill(("phone", "06 47 21 85 39"));
        _fields.SimulateUserTyping("phone", "0647218539");
        using var service = StartService();

        _listener.SimulatePress();

        Assert.Equal(string.Empty, _fields.CurrentValues["phone"]);
    }

    [Fact]
    public void HotkeyPressed_WhenFieldCannotBeReadAnymore_ReportsIt()
    {
        Fill(("email", "antoine.b@orange.fr"));
        _fields.UnreadableFieldIds.Add("email");
        using var service = StartService();

        _listener.SimulatePress();

        Assert.Equal("antoine.b@orange.fr", _fields.CurrentValues["email"]);
        Assert.Equal(
            "Annulé : 1 impossible à vider.\nLe formulaire a sans doute été fermé.",
            Assert.Single(_notifier.Messages));
    }

    [Fact]
    public void HotkeyPressed_WhenFieldRefusesToBeEmptied_ReportsIt()
    {
        Fill(("email", "antoine.b@orange.fr"), ("city", "Reims"));
        _fields.FailingFieldIds.Add("city");
        using var service = StartService();

        _listener.SimulatePress();

        Assert.Equal("Annulé : 1 champ vidé, 1 impossible à vider.", Assert.Single(_notifier.Messages));
    }

    [Fact]
    public void HotkeyPressed_ReportsTheValuesUndoneOnly()
    {
        IReadOnlyList<WrittenField>? undone = null;
        _fillMemory.Undone += (_, written) => undone = written;
        Fill(("email", "antoine.b@orange.fr"), ("city", "Reims"));
        _fields.SimulateUserTyping("city", "Taissy");
        using var service = StartService();

        _listener.SimulatePress();

        Assert.NotNull(undone);
        Assert.Equal("email", Assert.Single(undone).Field.Id);
    }

    [Fact]
    public void HotkeyPressed_TwiceInARow_UndoesOnlyOnce()
    {
        Fill(("email", "antoine.b@orange.fr"));
        using var service = StartService();
        _listener.SimulatePress();
        _fields.SimulateUserTyping("email", "antoine.b@orange.fr");

        _listener.SimulatePress();

        Assert.Equal("antoine.b@orange.fr", _fields.CurrentValues["email"]);
        Assert.Equal("Rien à annuler.", _notifier.Messages[^1]);
    }

    [Fact]
    public void HotkeyPressed_WithoutFill_SaysThereIsNothingToUndo()
    {
        using var service = StartService();

        _listener.SimulatePress();

        Assert.Equal("Rien à annuler.", Assert.Single(_notifier.Messages));
    }

    [Fact]
    public void HotkeyPressed_BeforeStart_DoesNothing()
    {
        Fill(("email", "antoine.b@orange.fr"));
        using var service = CreateService();

        _listener.SimulatePress();

        Assert.Empty(_notifier.Messages);
        Assert.Equal("antoine.b@orange.fr", _fields.CurrentValues["email"]);
    }

    [Fact]
    public void HotkeyPressed_AfterDispose_DoesNothing()
    {
        Fill(("email", "antoine.b@orange.fr"));
        var service = StartService();
        service.Dispose();

        _listener.SimulatePress();

        Assert.Empty(_notifier.Messages);
        Assert.Null(service.ActiveHotkey);
    }

    [Fact]
    public void HotkeyPressed_UndoesWhatTheFillServiceWrote()
    {
        using var fillListener = new FakeHotkeyListener();
        var formReader = new FakeFormReader
        {
            Fields = new[]
            {
                TestFields.Text("email", "Email"),
                TestFields.Text("name", "Nom", hasValue: true),
                TestFields.Text("misc", "Commentaire"),
            },
        };
        var source = new FakeCandidateSource
        {
            Candidates = new[]
            {
                new Candidate { Value = "antoine.b@orange.fr", Kind = CandidateKind.Email, Label = "Courriel", SourceName = "Gmail", SourceRank = 1 },
            },
        };
        using var fill = new ShortcutActivationService(fillListener, formReader, _fields, new[] { source }, _notifier, _fillMemory, new FakeFieldHighlighter());
        fill.Start(Hotkey.Default);
        using var undo = StartService();
        fillListener.SimulatePress();

        _listener.SimulatePress();

        Assert.Equal(string.Empty, _fields.CurrentValues["email"]);
        Assert.Equal(new[] { "email" }, _fields.CurrentValues.Keys);
        Assert.Equal("Annulé : 1 champ vidé.", _notifier.Messages[^1]);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private void Fill(params (string Id, string Value)[] values)
    {
        foreach (var (id, value) in values)
        {
            _fields.SimulateUserTyping(id, value);
        }

        _fillMemory.Remember(values
            .Select(entry => new WrittenField { Field = TestFields.Text(entry.Id, entry.Id), Value = entry.Value })
            .ToList());
    }

    private UndoShortcutService CreateService()
    {
        return new UndoShortcutService(_listener, _fillMemory, _fields, _fields, _notifier);
    }

    private UndoShortcutService StartService()
    {
        var service = CreateService();
        service.Start(Hotkey.DefaultUndo);
        return service;
    }
}
