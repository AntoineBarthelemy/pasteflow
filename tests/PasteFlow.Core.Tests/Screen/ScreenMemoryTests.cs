using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Screen;

/// <summary>
/// The memory of the screen (decision of Antoine of 14/11/2026): a text seen a moment ago, no longer on screen, is
/// found again in the list of Ctrl+Alt+V.
/// </summary>
public sealed class ScreenMemoryTests
{
    private const string SentMessage = "Peux-tu relire le devis DEV-2291 avant vendredi et me dire si la remise de 8 % te va ?";

    private readonly FakeTimeProvider _clock = new();

    [Fact]
    public void Recent_GivesTheTextsSeenNewestFirst()
    {
        var memory = new ScreenMemory(_clock);
        memory.Remember(Window("Bloc-notes", "Commande CMD-4471"));
        _clock.Now += TimeSpan.FromMinutes(1);
        memory.Remember(Window("Claude", SentMessage));

        Assert.Equal(new[] { "Claude", "Bloc-notes" }, memory.Recent().Select(recent => recent.Window.Title));
    }

    [Fact]
    public void Recent_ForgetsTheTextsSeenMoreThanThirtyMinutesAgo()
    {
        var memory = new ScreenMemory(_clock);
        memory.Remember(Window("Claude", SentMessage));

        _clock.Now += ScreenMemory.KeepFor + TimeSpan.FromSeconds(1);

        Assert.Empty(memory.Recent());
    }

    [Fact]
    public void Remember_KeepsTwentyTextsAtMost()
    {
        var memory = new ScreenMemory(_clock);
        for (var index = 0; index < ScreenMemory.MaxTexts + 5; index++)
        {
            memory.Remember(Window($"Fenêtre {index}", $"Texte numéro {index}"));
        }

        var recent = memory.Recent();
        Assert.Equal(ScreenMemory.MaxTexts, recent.Count);
        Assert.Equal("Fenêtre 24", recent[0].Window.Title);
    }

    [Fact]
    public void Remember_KeepsAConversationGoingOnOnce()
    {
        var memory = new ScreenMemory(_clock);
        memory.Remember(Window("Claude", "Bonjour"));
        memory.Remember(Window("Claude", "Bonjour\n" + SentMessage));
        memory.Remember(Window("Claude", SentMessage));

        Assert.Equal("Bonjour\n" + SentMessage, Assert.Single(memory.Recent()).Window.Text);
    }

    [Theory]
    [InlineData("Coffre - Bitwarden", "")]
    [InlineData("Nouvel onglet - Navigation InPrivate - Microsoft Edge", "msedge")]
    [InlineData("Nouvel onglet - Google Chrome (Navigation privée)", "chrome")]
    [InlineData("Mots de passe", "keepassxc")]
    public void Remember_NeverKeepsAVaultOrAPrivateWindow(string title, string program)
    {
        var memory = new ScreenMemory(_clock);

        memory.Remember(Window(title, "identifiant : lea.martin", program));

        Assert.Empty(memory.Recent());
    }

    [Fact]
    public void Clear_And_SwitchingOff_ForgetEverything()
    {
        var memory = new ScreenMemory(_clock);
        memory.Remember(Window("Claude", SentMessage));
        memory.Clear();
        Assert.Empty(memory.Recent());

        memory.Remember(Window("Claude", SentMessage));
        memory.IsEnabled = false;
        memory.Remember(Window("Bloc-notes", "Commande CMD-4471"));

        Assert.Empty(memory.Recent());
    }

    [Fact]
    public void AsWindows_LeavesOutWhatIsStillOnScreenAndNamesTheRest()
    {
        var memory = new ScreenMemory(_clock);
        memory.Remember(Window("Claude", SentMessage));
        memory.Remember(Window("Bloc-notes", "Commande CMD-4471"));

        var windows = memory.AsWindows(new[] { new WindowText { Title = "Bloc-notes", Text = "Commande CMD-4471", Rank = 0 } });

        var claude = Assert.Single(windows);
        Assert.StartsWith("Vu récemment : Claude (", claude.Title, StringComparison.Ordinal);
        Assert.Equal(1, claude.Rank);
    }

    [Fact]
    public void ListAtTheCursor_FindsAMessageSeenInAConversationLeftSince()
    {
        // Antoine's example of 13/11/2026: a message sent in the wrong conversation, to paste in the right one
        var memory = new ScreenMemory(_clock);
        var formReader = new FakeFormReader { Fields = new[] { TestFields.Text("message", "Message") } };
        var caret = new FakeCaret { Caret = new CaretContext { IsEditable = true, FocusedFieldId = "message", FieldLabel = "Message" } };
        caret.Choose = request =>
        {
            var state = new PickerState(request);
            state.SetQuery("relire le devis");
            state.NextCut();
            return state.Selected?.Text;
        };

        Press(memory, formReader, caret, new WindowText { Title = "Claude", Text = "Conversation : devis\n" + SentMessage, Rank = 0 });
        _clock.Now += TimeSpan.FromMinutes(2);
        Press(memory, formReader, caret, new WindowText { Title = "Claude", Text = "Conversation : planning\nRien d'autre ici.", Rank = 0 });

        Assert.Equal(SentMessage, caret.InsertedTexts[^1]);
        var source = new PickerState(caret.ShownRequest!);
        source.SetQuery("relire le devis");
        Assert.StartsWith("Vu récemment : Claude", source.Selected!.SourceName, StringComparison.Ordinal);
    }

    [Fact]
    public void ListAtTheCursor_WithoutMemory_DoesNotFindIt()
    {
        var formReader = new FakeFormReader { Fields = new[] { TestFields.Text("message", "Message") } };
        var caret = new FakeCaret { Caret = new CaretContext { IsEditable = true, FocusedFieldId = "message", FieldLabel = "Message" } };
        caret.Choose = request =>
        {
            var state = new PickerState(request);
            state.SetQuery("relire le devis");
            return state.Selected?.Text;
        };

        Press(null, formReader, caret, new WindowText { Title = "Claude", Text = "Conversation : devis\n" + SentMessage, Rank = 0 });
        Press(null, formReader, caret, new WindowText { Title = "Claude", Text = "Conversation : planning\nRien d'autre ici.", Rank = 0 });

        Assert.Single(caret.InsertedTexts);
    }

    private static WindowText Window(string title, string text, string program = "")
    {
        return new WindowText { Title = title, Text = text, Rank = 0, Program = program };
    }

    private static void Press(ScreenMemory? memory, FakeFormReader formReader, FakeCaret caret, params WindowText[] windows)
    {
        using var listener = new FakeHotkeyListener();
        using var service = new ShortcutActivationService(
            listener,
            formReader,
            new FakeFieldWriter(),
            new ICandidateSource[] { new ScreenCandidateSource(new FakeWindowTextReader { Windows = windows }) },
            new FakeUserNotifier(),
            new FillMemory(),
            new FakeFieldHighlighter())
        {
            CaretReader = caret,
            Picker = caret,
            Inserter = caret,
            Memory = memory,
        };
        service.Start(Hotkey.Default);

        listener.SimulatePress();
    }
}
