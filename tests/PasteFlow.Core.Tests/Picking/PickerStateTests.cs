using PasteFlow.Core.Candidates;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Tests.Picking;

public sealed class PickerStateTests
{
    private const string Text = "Le mieux est d'écrire d'abord à Léa. Elle gère l'agenda d'Hugo et répond en général sous 48 h.";

    private static readonly Candidate[] Suggestions =
    {
        new() { Value = "lea.martin@atelier-nova.fr", Kind = CandidateKind.Email, SourceName = "Claude", SourceRank = 1 },
        new() { Value = "06 12 48 30 77", Kind = CandidateKind.Phone, SourceName = "Claude", SourceRank = 1 },
    };

    private readonly PickerState _state = new(new PickRequest
    {
        Suggestions = Suggestions,
        Finder = new PassageFinder(new[] { new WindowText { Title = "Claude", Rank = 1, Text = Text } }, Suggestions),
    });

    [Fact]
    public void New_ShowsTheLikelyValuesWithWhatTheyAre()
    {
        Assert.False(_state.IsSearching);
        Assert.Equal(new[] { "lea.martin@atelier-nova.fr", "06 12 48 30 77" }, _state.Items.Select(item => item.Text));
        Assert.Equal(new[] { "e-mail", "téléphone" }, _state.Items.Select(item => item.Description));
        Assert.Equal(0, _state.SelectedIndex);
    }

    [Fact]
    public void SetQuery_ShowsThePassagesCutAtTheTypedWords()
    {
        _state.SetQuery("agenda d'hu");

        Assert.True(_state.IsSearching);
        Assert.Equal("agenda d'Hugo", Assert.Single(_state.Items).Text);
    }

    [Fact]
    public void SetQuery_BackToNothing_ShowsTheLikelyValuesAgain()
    {
        _state.SetQuery("agenda");
        _state.SetQuery(" ");

        Assert.Equal(2, _state.Items.Count);
    }

    [Fact]
    public void SetQuery_WithNothingFound_HasNoSelection()
    {
        _state.SetQuery("introuvable");

        Assert.Empty(_state.Items);
        Assert.Equal(-1, _state.SelectedIndex);
        Assert.Null(_state.Selected);
    }

    [Fact]
    public void NextCut_WidensToTheSentenceThenTheLineThenTheListThenThePageThenBack()
    {
        _state.SetQuery("agenda");

        _state.NextCut();
        Assert.Equal(PassageCut.Sentence, _state.Cut);
        Assert.Equal("Elle gère l'agenda d'Hugo et répond en général sous 48 h.", _state.Selected?.Text);

        _state.NextCut();
        Assert.Equal(Text, _state.Selected?.Text);

        // Outside a list, the list cut keeps the line
        _state.NextCut();
        Assert.Equal(PassageCut.List, _state.Cut);
        Assert.Equal(Text, _state.Selected?.Text);

        _state.NextCut();
        Assert.Equal(PassageCut.Page, _state.Cut);

        _state.NextCut();
        Assert.Equal(PassageCut.Words, _state.Cut);
        Assert.Equal("agenda", _state.Selected?.Text);
    }

    [Fact]
    public void MoveSelection_StaysInsideTheList()
    {
        _state.MoveSelection(1);
        _state.MoveSelection(1);
        Assert.Equal(1, _state.SelectedIndex);

        _state.MoveSelection(-5);
        Assert.Equal(0, _state.SelectedIndex);
    }

    [Fact]
    public void ExtendAndShrink_AdjustTheHighlightedPassageWordByWord()
    {
        _state.SetQuery("elle gere");

        _state.ExtendEnd();
        _state.ExtendEnd();
        Assert.Equal("Elle gère l'agenda d'Hugo", _state.Selected?.Text);

        _state.ShrinkEnd();
        _state.ExtendStart();
        Assert.Equal("Léa. Elle gère l'agenda", _state.Selected?.Text);

        _state.ShrinkStart();
        Assert.Equal("Elle gère l'agenda", _state.Selected?.Text);
    }

    [Fact]
    public void Adjusting_AValue_ChangesNothing()
    {
        _state.ExtendEnd();

        Assert.Equal("lea.martin@atelier-nova.fr", _state.Selected?.Text);
    }

    [Fact]
    public void SetQuery_OffersTheAddressBehindALinkWhoseTextMatches()
    {
        var link = new Candidate
        {
            Value = "https://www.youtube.com/watch?v=abc123",
            Kind = CandidateKind.Url,
            Label = "Ma vidéo de rentrée",
            SourceName = "YouTube",
            SourceRank = 1,
            IsLinkAddress = true,
        };
        var state = new PickerState(new PickRequest
        {
            Suggestions = Suggestions,
            Finder = new PassageFinder(new[] { new WindowText { Title = "YouTube", Rank = 1, Text = "Ma vidéo de rentrée" } }, Suggestions),
            Links = new[] { link },
        });

        state.SetQuery("video");

        Assert.Equal(new[] { "vidéo", "https://www.youtube.com/watch?v=abc123" }, state.Items.Select(item => item.Text));
        Assert.Equal("lien : Ma vidéo de rentrée", state.Items[1].Description);
    }
}
