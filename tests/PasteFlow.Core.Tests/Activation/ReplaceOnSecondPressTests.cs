using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Activation;

/// <summary>
/// A second press of the shortcut on the same form replaces the values already there, when sure enough.
/// </summary>
public sealed class ReplaceOnSecondPressTests : IDisposable
{
    private const string ReplaceHint = "Ctrl+Alt+V à nouveau pour remplacer les champs déjà remplis";

    private static readonly FormField[] StudioForm =
    {
        TestFields.Text("search", "Search across your channel", hasValue: true),
        TestFields.Text("title", "Title (required)", hasValue: true),
        TestFields.Text("description", "Description", hasValue: true),
    };

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeHotkeyListener _undoListener = new();
    private readonly FakeFormReader _formReader = new() { Fields = StudioForm };
    private readonly FakeFieldWriter _fieldWriter = new();
    private readonly FakeCandidateSource _candidateSource = new();
    private readonly FakeUserNotifier _notifier = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly FillMemory _fillMemory = new();

    public ReplaceOnSecondPressTests()
    {
        _fieldWriter.SimulateUserTyping("search", "tuto");
        _fieldWriter.SimulateUserTyping("title", "Ancien titre");
        _fieldWriter.SimulateUserTyping("description", "Ancienne description");
        _candidateSource.Candidates = new[]
        {
            FromField("tutoriel", "Search across your channel"),
            FromField("Mon tuto PasteFlow", "Title (required)"),
            FromField("Dans cette vidéo, je montre PasteFlow.\nContact : antoine@exemple.fr", "Description"),
        };
    }

    [Fact]
    public void FirstPress_LeavesFilledFieldsAndTellsHowToReplaceThem()
    {
        using var service = CreateService();

        _listener.SimulatePress();

        Assert.Empty(_fieldWriter.WrittenValues);
        var message = Assert.Single(_notifier.Messages);
        Assert.Contains("2 déjà remplis laissés intacts", message, StringComparison.Ordinal);
        Assert.Contains(ReplaceHint, message, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondPress_ReplacesValuesSureEnough()
    {
        using var service = CreateService();

        PressTwice(TimeSpan.FromSeconds(3));

        Assert.Equal("Mon tuto PasteFlow", _fieldWriter.CurrentValues["title"]);
        Assert.Equal("Dans cette vidéo, je montre PasteFlow.\nContact : antoine@exemple.fr", _fieldWriter.CurrentValues["description"]);
        Assert.Contains("2 champs remplacés", _notifier.Messages[1], StringComparison.Ordinal);
        Assert.DoesNotContain(ReplaceHint, _notifier.Messages[1], StringComparison.Ordinal);
    }

    [Fact]
    public void SecondPress_NeverReplacesASearchBox()
    {
        using var service = CreateService();

        PressTwice(TimeSpan.FromSeconds(1));

        Assert.Equal("tuto", _fieldWriter.CurrentValues["search"]);
    }

    [Fact]
    public void SecondPress_TooLate_LeavesFilledFieldsIntact()
    {
        using var service = CreateService();

        PressTwice(ShortcutActivationService.ReplaceDelay + TimeSpan.FromSeconds(1));

        Assert.Empty(_fieldWriter.WrittenValues);
        Assert.Contains(ReplaceHint, _notifier.Messages[1], StringComparison.Ordinal);
    }

    [Fact]
    public void SecondPress_OnAnotherForm_LeavesFilledFieldsIntact()
    {
        using var service = CreateService();
        _listener.SimulatePress();
        _formReader.Fields = new[] { TestFields.Text("title", "Title (required)", hasValue: true) };
        _clock.Now += TimeSpan.FromSeconds(2);

        _listener.SimulatePress();

        Assert.Empty(_fieldWriter.WrittenValues);
    }

    [Fact]
    public void SecondPress_WithValueNotSureEnough_KeepsTheValueOfTheUser()
    {
        _candidateSource.Candidates = new[]
        {
            new Candidate { Value = "Un titre sans étiquette", SourceName = "Bloc-notes", SourceRank = 1 },
        };
        using var service = CreateService();

        PressTwice(TimeSpan.FromSeconds(2));

        Assert.Empty(_fieldWriter.WrittenValues);
        Assert.Equal("Ancien titre", _fieldWriter.CurrentValues["title"]);
    }

    [Fact]
    public void SecondPress_WithTheSameValueAlreadyThere_WritesNothing()
    {
        _fieldWriter.SimulateUserTyping("title", "Mon tuto PasteFlow");
        using var service = CreateService();

        PressTwice(TimeSpan.FromSeconds(2));

        Assert.False(_fieldWriter.WrittenValues.ContainsKey("title"));
        Assert.Contains("1 champ remplacé, 1 déjà rempli laissé intact", _notifier.Messages[1], StringComparison.Ordinal);
    }

    [Fact]
    public void SecondPress_WhenTheCurrentValueCannotBeRead_LeavesTheField()
    {
        _ = _fieldWriter.UnreadableFieldIds.Add("title");
        using var service = CreateService();

        PressTwice(TimeSpan.FromSeconds(2));

        Assert.False(_fieldWriter.WrittenValues.ContainsKey("title"));
        Assert.True(_fieldWriter.WrittenValues.ContainsKey("description"));
    }

    [Fact]
    public void SecondPress_WhenValuesCannotBeReadBack_NeverReplaces()
    {
        var writer = new WriteOnlyFieldWriter();
        using var service = new ShortcutActivationService(
            _listener,
            _formReader,
            writer,
            new[] { _candidateSource },
            _notifier,
            _fillMemory,
            new FakeFieldHighlighter())
        {
            TimeProvider = _clock,
        };
        service.Start(Hotkey.Default);

        PressTwice(TimeSpan.FromSeconds(2));

        Assert.Empty(writer.WrittenValues);
    }

    [Fact]
    public void SecondPress_WhenThePageShowsTheValueBeingReplaced_StillReplacesIt()
    {
        // A browser shows the value of each field in the text of the page, read as the form's own window
        _candidateSource.Candidates = _candidateSource.Candidates
            .Append(new Candidate { Value = "Ancien titre", Label = "Title (required)", SourceName = "Studio", SourceRank = 0 })
            .ToArray();
        using var service = CreateService();

        PressTwice(TimeSpan.FromSeconds(2));

        Assert.Equal("Mon tuto PasteFlow", _fieldWriter.CurrentValues["title"]);
    }

    [Fact]
    public void SecondPress_StillFillsEmptyFields()
    {
        _formReader.Fields = StudioForm.Append(TestFields.Text("email", "Email")).ToArray();
        _candidateSource.Candidates = _candidateSource.Candidates
            .Append(new Candidate { Value = "antoine.b@orange.fr", Kind = CandidateKind.Email, Label = "Email", SourceName = "Gmail", SourceRank = 1 })
            .ToArray();
        using var service = CreateService();

        PressTwice(TimeSpan.FromSeconds(2));

        Assert.Equal("antoine.b@orange.fr", _fieldWriter.CurrentValues["email"]);
        Assert.Equal("Mon tuto PasteFlow", _fieldWriter.CurrentValues["title"]);
    }

    [Fact]
    public void Undo_AfterReplacing_PutsThePreviousValuesBack()
    {
        using var service = CreateService();
        using var undo = new UndoShortcutService(_undoListener, _fillMemory, _fieldWriter, _fieldWriter, _notifier);
        undo.Start(Hotkey.DefaultUndo);
        PressTwice(TimeSpan.FromSeconds(2));

        _undoListener.SimulatePress();

        Assert.Equal("Ancien titre", _fieldWriter.CurrentValues["title"]);
        Assert.Equal("Ancienne description", _fieldWriter.CurrentValues["description"]);
        Assert.Equal("Annulé : 2 remis comme avant.", _notifier.Messages[^1]);
    }

    [Fact]
    public void Undo_AfterReplacing_LeavesAFieldTheUserChangedSince()
    {
        using var service = CreateService();
        using var undo = new UndoShortcutService(_undoListener, _fillMemory, _fieldWriter, _fieldWriter, _notifier);
        undo.Start(Hotkey.DefaultUndo);
        PressTwice(TimeSpan.FromSeconds(2));
        _fieldWriter.SimulateUserTyping("title", "Titre retouché");

        _undoListener.SimulatePress();

        Assert.Equal("Titre retouché", _fieldWriter.CurrentValues["title"]);
        Assert.Equal("Ancienne description", _fieldWriter.CurrentValues["description"]);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _undoListener.Dispose();
    }

    private static Candidate FromField(string value, string label)
    {
        return new Candidate { Value = value, Label = label, SourceName = "YouTube Studio", SourceRank = 1, IsTypedInField = true };
    }

    private void PressTwice(TimeSpan interval)
    {
        _listener.SimulatePress();
        _clock.Now += interval;
        _listener.SimulatePress();
    }

    private ShortcutActivationService CreateService()
    {
        var service = new ShortcutActivationService(
            _listener,
            _formReader,
            _fieldWriter,
            new[] { _candidateSource },
            _notifier,
            _fillMemory,
            new FakeFieldHighlighter())
        {
            TimeProvider = _clock,
            UndoHotkey = Hotkey.DefaultUndo,
        };
        service.Start(Hotkey.Default);
        return service;
    }

    /// <summary>
    /// Fields that can be written but not read back, as some applications do.
    /// </summary>
    private sealed class WriteOnlyFieldWriter : IFieldWriter
    {
        public Dictionary<string, string> WrittenValues { get; } = new();

        public Task<bool> TryWriteAsync(FormField field, string value, CancellationToken cancellationToken)
        {
            WrittenValues[field.Id] = value;
            return Task.FromResult(true);
        }
    }
}
