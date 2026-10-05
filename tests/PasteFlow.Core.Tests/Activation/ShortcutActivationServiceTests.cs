using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Activation;

public sealed class ShortcutActivationServiceTests : IDisposable
{
    private static readonly string[] ExpectedFilledFieldIds = { "city", "name" };

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeFormReader _formReader = new();
    private readonly FakeFieldWriter _fieldWriter = new();
    private readonly FakeCandidateSource _candidateSource = new();
    private readonly FakeUserNotifier _notifier = new();

    [Fact]
    public void Start_WhenRegistrationSucceeds_ListensToHotkey()
    {
        using var service = CreateService();

        var isStarted = service.Start(Hotkey.Default);

        Assert.True(isStarted);
        Assert.Equal(Hotkey.Default, _listener.RegisteredHotkey);
        Assert.Equal(Hotkey.Default, service.ActiveHotkey);
    }

    [Fact]
    public void Start_WhenHotkeyIsAlreadyUsed_ReturnsFalse()
    {
        var listener = new FakeHotkeyListener { AcceptsRegistration = false };
        using var service = new ShortcutActivationService(listener, _formReader, _fieldWriter, new[] { _candidateSource }, _notifier);

        var isStarted = service.Start(Hotkey.Default);

        Assert.False(isStarted);
        Assert.Null(service.ActiveHotkey);
    }

    [Fact]
    public void Start_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        using var service = CreateService();
        service.Start(Hotkey.Default);

        Assert.Throws<InvalidOperationException>(() => service.Start(Hotkey.Default));
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = CreateService();
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Start(Hotkey.Default));
    }

    [Fact]
    public void HotkeyPressed_FillsOnlyEmptyTextFields()
    {
        var formReader = new FakeFormReader
        {
            Fields = new[]
            {
                TestFields.Text("name", "Nom"),
                TestFields.Text("email", "Email", hasValue: true),
                TestFields.Password("password", "Mot de passe"),
                TestFields.Choice("country", "Pays"),
                TestFields.Text("city", "Ville"),
            },
        };
        using var service = new ShortcutActivationService(_listener, formReader, _fieldWriter, new[] { _candidateSource }, _notifier);
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        Assert.Equal(ExpectedFilledFieldIds, _fieldWriter.WrittenValues.Keys.Order());
    }

    [Fact]
    public void HotkeyPressed_WritesLabelAsDemoValue()
    {
        var formReader = new FakeFormReader
        {
            Fields = new[] { TestFields.Text("name", "Nom"), TestFields.Text("unnamed", string.Empty) },
        };
        using var service = new ShortcutActivationService(_listener, formReader, _fieldWriter, new[] { _candidateSource }, _notifier);
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        Assert.Equal("Nom", _fieldWriter.WrittenValues["name"]);
        Assert.Equal("PasteFlow", _fieldWriter.WrittenValues["unnamed"]);
    }

    [Fact]
    public void HotkeyPressed_NotifiesReportWithFailures()
    {
        var formReader = new FakeFormReader
        {
            Fields = new[]
            {
                TestFields.Text("name", "Nom"),
                TestFields.Text("email", "Email", hasValue: true),
                TestFields.Text("broken", "Ville"),
            },
        };
        _fieldWriter.FailingFieldIds.Add("broken");
        using var service = new ShortcutActivationService(_listener, formReader, _fieldWriter, new[] { _candidateSource }, _notifier);
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        var message = Assert.Single(_notifier.Messages);
        Assert.StartsWith(
            "1 champ rempli, 1 déjà rempli laissé intact, 1 champ impossible à remplir.",
            message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyPressed_ReportsCandidatesBySourceClosestFirst()
    {
        _candidateSource.Candidates = new[]
        {
            CandidateFrom("Bloc-notes", rank: 1, "CMD-48213"),
            CandidateFrom("Gmail", rank: 0, "Bonjour"),
            CandidateFrom("Gmail", rank: 0, "Commande n° CMD-48213"),
        };
        using var service = CreateService();
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        Assert.Equal(1, _candidateSource.ReadCount);
        var message = Assert.Single(_notifier.Messages);
        Assert.EndsWith("Lu à l'écran : Gmail (2), Bloc-notes (1).", message, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyPressed_WhenWindowHasNoField_ReportsNothingToFill()
    {
        using var service = CreateService();
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        Assert.Empty(_fieldWriter.WrittenValues);
        var message = Assert.Single(_notifier.Messages);
        Assert.StartsWith("Aucun champ à remplir dans cette fenêtre.", message, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyPressed_WithoutSource_ReportsNothingRead()
    {
        using var service = new ShortcutActivationService(
            _listener,
            _formReader,
            _fieldWriter,
            Array.Empty<ICandidateSource>(),
            _notifier);
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        var message = Assert.Single(_notifier.Messages);
        Assert.EndsWith("Rien lu à l'écran.", message, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyPressed_WithSeveralSources_ReadsAllOfThem()
    {
        var otherSource = new FakeCandidateSource
        {
            Candidates = new[] { CandidateFrom("Historique", rank: 5, "CMD-11111") },
        };
        _candidateSource.Candidates = new[] { CandidateFrom("Gmail", rank: 0, "Bonjour") };
        using var service = new ShortcutActivationService(
            _listener,
            _formReader,
            _fieldWriter,
            new[] { _candidateSource, otherSource },
            _notifier);
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        Assert.Equal(1, otherSource.ReadCount);
        var message = Assert.Single(_notifier.Messages);
        Assert.EndsWith("Lu à l'écran : Gmail (1), Historique (1).", message, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyPressed_WithSameTitleInTwoWindows_ListsThemSeparately()
    {
        _candidateSource.Candidates = new[]
        {
            CandidateFrom("Nouvel onglet", rank: 0, "A"),
            CandidateFrom("Nouvel onglet", rank: 1, "B"),
        };
        using var service = CreateService();
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        var message = Assert.Single(_notifier.Messages);
        Assert.EndsWith("Lu à l'écran : Nouvel onglet (1), Nouvel onglet (1).", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HotkeyPressed_ReadsSourcesWhileTheFormIsBeingRead()
    {
        var pendingReading = new TaskCompletionSource<IReadOnlyList<FormField>>();
        var formReader = new FakeFormReader { PendingReading = pendingReading };
        using var service = new ShortcutActivationService(
            _listener,
            formReader,
            _fieldWriter,
            new[] { _candidateSource },
            _notifier);
        service.Start(Hotkey.Default);

        _listener.SimulatePress();

        Assert.Equal(1, _candidateSource.ReadCount);
        Assert.Empty(_notifier.Messages);

        pendingReading.SetResult(new[] { TestFields.Text("name", "Nom") });
        var message = await _notifier.FirstMessage.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.StartsWith("1 champ rempli.", message, StringComparison.Ordinal);
        Assert.Equal("Nom", _fieldWriter.WrittenValues["name"]);
    }

    [Fact]
    public void HotkeyPressed_BeforeStart_DoesNothing()
    {
        using var service = CreateService();

        _listener.SimulatePress();

        Assert.Equal(0, _formReader.ReadCount);
        Assert.Empty(_notifier.Messages);
    }

    [Fact]
    public void HotkeyPressed_AfterDispose_DoesNothing()
    {
        var service = CreateService();
        service.Start(Hotkey.Default);
        service.Dispose();

        _listener.SimulatePress();

        Assert.Equal(0, _formReader.ReadCount);
        Assert.Empty(_notifier.Messages);
    }

    [Fact]
    public void Dispose_DoesNotDisposeListenerOwnedByCaller()
    {
        var service = CreateService();

        service.Dispose();

        Assert.False(_listener.IsDisposed);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private static Candidate CandidateFrom(string sourceName, int rank, string value)
    {
        return new Candidate { Value = value, SourceName = sourceName, SourceRank = rank };
    }

    private ShortcutActivationService CreateService()
    {
        return new ShortcutActivationService(_listener, _formReader, _fieldWriter, new[] { _candidateSource }, _notifier);
    }
}
