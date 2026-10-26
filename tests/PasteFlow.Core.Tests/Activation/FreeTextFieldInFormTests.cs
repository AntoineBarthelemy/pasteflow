using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Activation;

/// <summary>
/// The shortcut in a box of free text of a form, such as instructions: no value fits it, the list at the cursor opens.
/// </summary>
public sealed class FreeTextFieldInFormTests : IDisposable
{
    private const string Draft = """
        lea.martin@atelier-nova.fr
        Pas de tiret cadratin ni de point médian. Je préfère des réponses claires et structurées.
        """;

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeFormReader _formReader = new()
    {
        Fields = new[] { TestFields.Text("email", "E-mail"), TestFields.Text("instructions", "Instructions for Claude") },
    };

    private readonly FakeFieldWriter _fields = new();
    private readonly FakeUserNotifier _notifier = new();
    private readonly FakeCaret _caret = new();
    private readonly FakeFieldHighlighter _highlighter = new();

    [Fact]
    public void Press_InAnEmptyBoxWhereNothingFits_FillsTheFormThenOpensTheListThere()
    {
        _caret.Caret = Caret("instructions");
        _caret.Choose = request => request.Finder.Find("pas de tiret", PassageCut.Line)[0].Text;

        Press();

        Assert.Equal("lea.martin@atelier-nova.fr", _fields.WrittenValues["email"]);
        Assert.NotNull(_caret.ShownRequest);
        Assert.StartsWith("Pas de tiret cadratin", Assert.Single(_caret.InsertedTexts), StringComparison.Ordinal);
        Assert.DoesNotContain(_highlighter.HighlightedFields, field => field.Field.Id == "instructions");
    }

    [Fact]
    public void Press_WithTheCursorInAnotherField_LeavesTheBoxToCheckWithoutTheList()
    {
        _caret.Caret = Caret("email");

        Press();

        Assert.Null(_caret.ShownRequest);
        Assert.Contains(_highlighter.HighlightedFields, field => field.Field.Id == "instructions");
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    private static CaretContext Caret(string fieldId)
    {
        return new CaretContext { IsEditable = true, FocusedFieldId = fieldId, FieldLabel = string.Empty, TextBefore = string.Empty };
    }

    private void Press()
    {
        var reader = new FakeWindowTextReader { Windows = new[] { new WindowText { Title = "Google", Rank = 1, Text = Draft } } };
        using var service = new ShortcutActivationService(
            _listener,
            _formReader,
            _fields,
            new ICandidateSource[] { new ScreenCandidateSource(reader) },
            _notifier,
            new FillMemory(),
            _highlighter)
        {
            CaretReader = _caret,
            Picker = _caret,
            Inserter = _caret,
        };
        service.Start(Hotkey.Default);

        _listener.SimulatePress();
    }
}
