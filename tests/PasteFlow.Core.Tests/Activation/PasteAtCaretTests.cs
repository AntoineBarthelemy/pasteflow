using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Activation;

/// <summary>
/// The shortcut in a text written alone, such as a message: it pastes at the cursor.
/// </summary>
public sealed class PasteAtCaretTests : IDisposable
{
    private const string Contacts = """
        Voici les contacts que j'ai trouvés :
        Léa Martin, responsable communication
        lea.martin@atelier-nova.fr
        06 12 48 30 77

        Hugo Bernard, fondateur
        hugo@atelier-nova.fr

        Elle gère l'agenda d'Hugo et répond en général sous 48 h.
        """;

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeFormReader _formReader = new() { Fields = new[] { TestFields.Text("message", "Message") } };
    private readonly FakeFieldWriter _fields = new();
    private readonly FakeUserNotifier _notifier = new();
    private readonly FakeCaret _caret = new();
    private readonly FakeFieldHighlighter _highlighter = new();
    private WindowText[] _windows = { new() { Title = "Claude", Rank = 1, Text = Contacts } };

    [Fact]
    public void Press_WhenTheWordsBeforeTheCursorAskForAValueOnScreen_PastesItAtTheCursor()
    {
        _windows = new[] { new WindowText { Title = "Claude", Rank = 1, Text = "Léa Martin\n06 12 48 30 77" } };
        _caret.Caret = Caret("Tu peux l'appeler, son téléphone : ");

        Press();

        Assert.Equal(new[] { "06 12 48 30 77" }, _caret.InsertedTexts);
        Assert.Null(_caret.ShownRequest);
        Assert.Empty(_fields.WrittenValues);
        Assert.Equal("Collé : 06 12 48 30 77 (Claude)", Assert.Single(_notifier.Messages));
        Assert.Equal(("06 12 48 30 77", false), Assert.Single(_caret.CopiedTexts));
    }

    [Fact]
    public void Press_WhenNothingIsSure_OffersTheLikelyValuesClosestFirst()
    {
        _caret.Caret = Caret("Tiens : ");

        Press();

        var request = Assert.IsType<PickRequest>(_caret.ShownRequest);
        Assert.Equal(
            new[] { "lea.martin@atelier-nova.fr", "06 12 48 30 77", "hugo@atelier-nova.fr" },
            request.Suggestions.Select(candidate => candidate.Value).Where(value => value.Contains('@') || value.StartsWith("06", StringComparison.Ordinal)));
        Assert.True(request.Suggestions.Count <= PassageFinder.MaxResults);
        Assert.Empty(_caret.InsertedTexts);
    }

    [Fact]
    public void Press_WhenTheWindowShowsTheTextBeingTyped_DoesNotTakeWhatFollowsItForItsValue()
    {
        // The words typed are on screen too, followed by the button under the box: "Tiens :" then "Envoyer"
        _windows = new[]
        {
            new WindowText { Title = "Messagerie", Rank = 0, Text = "Message à Paul\nTiens :\nEnvoyer" },
            new WindowText { Title = "Claude", Rank = 1, Text = Contacts },
        };
        _caret.Caret = Caret("Tiens : ");

        Press();

        var request = Assert.IsType<PickRequest>(_caret.ShownRequest);
        Assert.DoesNotContain(request.Suggestions, candidate => candidate.Value == "Envoyer");
        Assert.Empty(_caret.InsertedTexts);
    }

    [Fact]
    public void Press_WhenTheUserChoosesAValue_PastesIt()
    {
        _caret.Caret = Caret("Tiens : ");
        _caret.Choose = request => request.Suggestions.First(candidate => candidate.Kind == CandidateKind.Phone).Value;

        Press();

        Assert.Equal(new[] { "06 12 48 30 77" }, _caret.InsertedTexts);
        Assert.Empty(_notifier.Messages);
        Assert.Equal(("06 12 48 30 77", false), Assert.Single(_caret.CopiedTexts));
    }

    [Fact]
    public void Press_TheListSearchesTheTextOfTheWindows()
    {
        _caret.Caret = Caret("Tiens : ");
        _caret.Choose = request => request.Finder.Find("agenda d'hu", PassageCut.Words)[0].Text;

        Press();

        Assert.Equal(new[] { "agenda d'Hugo" }, _caret.InsertedTexts);
        Assert.Equal(("agenda d'Hugo", false), Assert.Single(_caret.CopiedTexts));
    }

    [Fact]
    public void Press_WhenTheUserCancels_PastesNothing()
    {
        _caret.Caret = Caret("Tiens : ");

        Press();

        Assert.Empty(_caret.InsertedTexts);
        Assert.Empty(_notifier.Messages);
        Assert.Empty(_caret.CopiedTexts);
    }

    [Fact]
    public void Press_WhenTheTextCannotBeWritten_SaysSo()
    {
        _caret.Caret = Caret("Tiens : ");
        _caret.Choose = _ => "agenda";
        _caret.AcceptsText = false;

        Press();

        Assert.Equal("Impossible d'écrire à cet endroit.", Assert.Single(_notifier.Messages));
        Assert.Empty(_caret.CopiedTexts);
    }

    [Fact]
    public void Press_InAFieldOfAForm_FillsTheFormInstead()
    {
        _formReader.Fields = new[] { TestFields.Text("name", "Nom"), TestFields.Text("email", "Email") };
        _windows = new[] { new WindowText { Title = "Mail", Rank = 1, Text = "Email : marie@boulangerie-martin.fr" } };
        _caret.Caret = new CaretContext { IsEditable = true, FocusedFieldId = "email" };

        Press();

        Assert.Equal("marie@boulangerie-martin.fr", _fields.WrittenValues["email"]);
        Assert.Null(_caret.ShownRequest);
        Assert.Empty(_caret.InsertedTexts);
    }

    [Fact]
    public void Press_ClearsTheFramesOfAPreviousFill()
    {
        _caret.Caret = Caret("Tiens : ");

        Press();

        Assert.Equal(1, _highlighter.CallCount);
        Assert.Empty(_highlighter.HighlightedFields);
    }

    public void Dispose()
    {
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
            new ICandidateSource[] { new ScreenCandidateSource(reader) },
            _notifier,
            new FillMemory(),
            _highlighter)
        {
            CaretReader = _caret,
            Picker = _caret,
            Inserter = _caret,
            Clipboard = _caret,
        };
        service.Start(Hotkey.Default);

        _listener.SimulatePress();
    }
}
