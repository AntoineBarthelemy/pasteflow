using PasteFlow.Core.Candidates;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Tests.Picking;

public sealed class PassageFinderTests
{
    private const string Reply = """
        Voici les contacts que j'ai trouvés :
        Léa Martin, responsable communication
        lea.martin@atelier-nova.fr
        06 12 48 30 77
        Le mieux est d'écrire d'abord à Léa. Elle gère l’agenda d'Hugo et répond en général sous 48 h.
        Total : 1 249,90 €
        """;

    private static readonly Candidate[] Values =
    {
        Value("lea.martin@atelier-nova.fr", CandidateKind.Email),
        Value("06 12 48 30 77", CandidateKind.Phone),
        Value("1 249,90 €", CandidateKind.Amount),
    };

    private readonly PassageFinder _finder = new(new[] { Window("Claude", 1, Reply) }, Values);

    [Theory]
    [InlineData("agenda d'hu", "agenda d'Hugo")]
    [InlineData("AGENDA D'HUGO", "agenda d'Hugo")]
    [InlineData("gere l'ag", "gère l’agenda")]
    [InlineData("responsable comm", "responsable communication")]
    public void Find_CompletesTheLastWordIgnoringCaseAccentsAndApostrophes(string query, string expected)
    {
        Assert.Equal(expected, First(query, PassageCut.Words));
    }

    [Theory]
    [InlineData("sous 48", "sous 48 h")]
    [InlineData("total : 1 249", "Total : 1 249,90 €")]
    public void Find_KeepsANumberWithItsUnit(string query, string expected)
    {
        Assert.Equal(expected, First(query, PassageCut.Words));
    }

    [Theory]
    [InlineData("lea.mar", "lea.martin@atelier-nova.fr")]
    [InlineData("06 12", "06 12 48 30 77")]
    public void Find_NeverCutsAValueInTwo(string query, string expected)
    {
        Assert.Equal(expected, First(query, PassageCut.Words));
    }

    [Fact]
    public void Find_OnlyMatchesTheStartOfWords()
    {
        Assert.DoesNotContain(_finder.Find("ugo", PassageCut.Words), passage => passage.Text.Contains("Hugo", StringComparison.Ordinal));
    }

    [Fact]
    public void Find_WithSentenceCut_TakesTheWholeSentence()
    {
        Assert.Equal("Elle gère l’agenda d'Hugo et répond en général sous 48 h.", First("agenda", PassageCut.Sentence));
        Assert.Equal("Le mieux est d'écrire d'abord à Léa.", First("d'abord", PassageCut.Sentence));
    }

    [Fact]
    public void Find_WithLineCut_TakesTheWholeLine()
    {
        Assert.Equal("Léa Martin, responsable communication", First("responsable", PassageCut.Line));
    }

    [Fact]
    public void Find_GivesTheWindowOfThePassage()
    {
        var passage = Assert.Single(_finder.Find("responsable", PassageCut.Words));

        Assert.Equal("Claude", passage.SourceName);
        Assert.Equal(1, passage.SourceRank);
    }

    [Fact]
    public void Find_OffersClosestWindowsFirstAndEachTextOnce()
    {
        var finder = new PassageFinder(
            new[] { Window("Loin", 2, "Facture de mars\nFacture de mars"), Window("Proche", 1, "Facture d'avril") },
            Array.Empty<Candidate>());

        var passages = finder.Find("facture", PassageCut.Line);

        Assert.Equal(new[] { "Facture d'avril", "Facture de mars" }, passages.Select(passage => passage.Text));
    }

    [Fact]
    public void Find_ListCut_TakesEveryItemOfTheNumberedList()
    {
        var text = "Colis préparés :\n1. Palette 1 : 24 cartons de verres\n2. Palette 2 : 18 cartons de carafes\n3. Palette 3 : 30 cartons\n\nEnlèvement le 12/11.";
        var finder = new PassageFinder(new[] { Window("Entrepôt", 1, text) }, Array.Empty<Candidate>());

        var passage = Assert.Single(finder.Find("palette 2", PassageCut.List));

        Assert.Equal("1. Palette 1 : 24 cartons de verres\n2. Palette 2 : 18 cartons de carafes\n3. Palette 3 : 30 cartons", passage.Text);
    }

    [Fact]
    public void Find_ListCut_TakesEveryItemOfTheBulletedList()
    {
        var finder = new PassageFinder(new[] { Window("Notes", 1, "À prévoir :\n- pain\n- lait\n- œufs") }, Array.Empty<Candidate>());

        Assert.Equal("- pain\n- lait\n- œufs", Assert.Single(finder.Find("lait", PassageCut.List)).Text);
    }

    [Fact]
    public void Find_ListCut_OutsideAList_TakesTheLine()
    {
        var finder = new PassageFinder(new[] { Window("Notes", 1, "Rendez-vous mardi à 10 h\nSalle B") }, Array.Empty<Candidate>());

        Assert.Equal("Rendez-vous mardi à 10 h", Assert.Single(finder.Find("mardi", PassageCut.List)).Text);
    }

    [Fact]
    public void Find_OffersAtMostFivePassages()
    {
        var text = string.Join('\n', Enumerable.Range(1, 9).Select(index => $"Ligne {index}"));
        var finder = new PassageFinder(new[] { Window("Notes", 1, text) }, Array.Empty<Candidate>());

        Assert.Equal(PassageFinder.MaxResults, finder.Find("ligne", PassageCut.Line).Count);
    }

    [Fact]
    public void Find_SearchesTheFilledFieldsToo()
    {
        var window = Window("Studio", 1, string.Empty) with
        {
            Fields = new[] { new FieldText { Label = "Description", Value = "Première ligne\nDans cette vidéo, je montre PasteFlow." } },
        };
        var finder = new PassageFinder(new[] { window }, Array.Empty<Candidate>());

        Assert.Equal("Dans cette vidéo", finder.Find("dans cette vi", PassageCut.Words)[0].Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("introuvable")]
    public void Find_WithNothingToFind_ReturnsNothing(string query)
    {
        Assert.Empty(_finder.Find(query, PassageCut.Words));
    }

    [Fact]
    public void ExtendAndShrinkEnd_MoveTheEndWordByWord()
    {
        var passage = _finder.Find("elle gere", PassageCut.Words)[0];

        var longer = PassageFinder.ExtendEnd(PassageFinder.ExtendEnd(passage));
        var shorter = PassageFinder.ShrinkEnd(longer);

        Assert.Equal("Elle gère", passage.Text);
        Assert.Equal("Elle gère l’agenda d'Hugo", longer.Text);
        Assert.Equal("Elle gère l’agenda", shorter.Text);
    }

    [Fact]
    public void ExtendAndShrinkStart_MoveTheStartWordByWord()
    {
        var passage = _finder.Find("agenda", PassageCut.Words)[0];

        var whole = PassageFinder.ExtendStart(passage);
        var longer = PassageFinder.ExtendStart(whole);
        var shorter = PassageFinder.ShrinkStart(longer);

        Assert.Equal("agenda", passage.Text);
        Assert.Equal("l’agenda", whole.Text);
        Assert.Equal("gère l’agenda", longer.Text);
        Assert.Equal("l’agenda", shorter.Text);
    }

    [Fact]
    public void Shrink_NeverLeavesAnEmptyPassage()
    {
        var passage = _finder.Find("responsable", PassageCut.Words)[0];

        Assert.Equal("responsable", PassageFinder.ShrinkEnd(passage).Text);
        Assert.Equal("responsable", PassageFinder.ShrinkStart(passage).Text);
    }

    [Fact]
    public void Extend_StopsAtTheEdgesOfTheLine()
    {
        var passage = _finder.Find("lea martin", PassageCut.Line)[0];

        Assert.Equal(passage.Text, PassageFinder.ExtendEnd(passage).Text);
        Assert.Equal(passage.Text, PassageFinder.ExtendStart(passage).Text);
    }

    [Fact]
    public void Find_StartingAtAnApostrophe_KeepsTheElidedWord()
    {
        Assert.Equal("l’agenda d'Hugo", First("l'agenda d'hu", PassageCut.Words));
    }

    [Fact]
    public void Empty_FindsNothing()
    {
        Assert.Empty(PassageFinder.Empty.Find("lea", PassageCut.Words));
    }

    [Fact]
    public void Find_ReadsAWindowWithEmoji()
    {
        var finder = new PassageFinder(new[] { Window("Discord", 1, "Merci Léa 👋 pour l'agenda d'Hugo 🎉") }, Values);

        Assert.Equal("agenda d'Hugo", finder.Find("agenda d'hu", PassageCut.Words)[0].Text);
    }

    [Fact]
    public void Find_ReadsAWindowWithHalfAnEmoji()
    {
        // A text cut in the middle of an emoji, as a window can give it
        var finder = new PassageFinder(new[] { Window("Discord", 1, "Rendez-vous avec Hugo \uD83D") }, Values);

        Assert.Equal("Rendez-vous avec Hugo", finder.Find("rendez-vous avec h", PassageCut.Words)[0].Text);
    }

    private static Candidate Value(string value, CandidateKind kind)
    {
        return new Candidate { Value = value, Kind = kind, SourceName = "Claude", SourceRank = 1 };
    }

    private static WindowText Window(string title, int rank, string text)
    {
        return new WindowText { Title = title, Rank = rank, Text = text };
    }

    private string First(string query, PassageCut cut)
    {
        return _finder.Find(query, cut)[0].Text;
    }
}
