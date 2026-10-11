using PasteFlow.Core.Candidates;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Candidates;

public sealed class ScreenCandidateSourceTests
{
    [Fact]
    public async Task GetCandidatesAsync_OffersEachPieceWithItsKindLabelAndWindow()
    {
        var reader = new FakeWindowTextReader
        {
            Windows = new[] { Window("Gmail", rank: 0, "Bonjour\nCommande n° CMD-48213") },
        };
        var source = new ScreenCandidateSource(reader);

        var candidates = await source.GetCandidatesAsync(CancellationToken.None);

        Assert.Collection(
            candidates,
            candidate => Assert.Equal(new Candidate { Value = "Bonjour", SourceName = "Gmail", SourceRank = 0 }, candidate),
            candidate => Assert.Equal(
                new Candidate
                {
                    Value = "CMD-48213",
                    Kind = CandidateKind.Reference,
                    Label = "Commande n°",
                    SourceName = "Gmail",
                    SourceRank = 0,
                },
                candidate),
            candidate => Assert.Equal("Commande n° CMD-48213", candidate.Value));
    }

    [Fact]
    public async Task GetCandidatesAsync_MarksValuesReadFromAnImage()
    {
        var reader = new FakeWindowTextReader
        {
            Windows = new[]
            {
                Window("Gmail", rank: 1, "Commande n° CMD-48213"),
                Window("Scan", rank: 2, "Facture FAC-2026-118") with { IsReadFromImage = true },
            },
        };

        var candidates = await new ScreenCandidateSource(reader).GetCandidatesAsync(CancellationToken.None);

        Assert.All(candidates.Where(candidate => candidate.SourceName == "Gmail"), candidate => Assert.False(candidate.IsReadFromImage));
        Assert.All(candidates.Where(candidate => candidate.SourceName == "Scan"), candidate => Assert.True(candidate.IsReadFromImage));
    }

    [Fact]
    public async Task GetCandidatesAsync_OrdersWindowsClosestFirst()
    {
        var reader = new FakeWindowTextReader
        {
            Windows = new[]
            {
                Window("Bloc-notes", rank: 2, "Note"),
                Window("Formulaire", rank: 0, "Nom"),
                Window("Gmail", rank: 1, "Bonjour"),
            },
        };
        var source = new ScreenCandidateSource(reader);

        var candidates = await source.GetCandidatesAsync(CancellationToken.None);

        Assert.Equal(new[] { "Formulaire", "Gmail", "Bloc-notes" }, candidates.Select(candidate => candidate.SourceName));
    }

    [Fact]
    public async Task GetCandidatesAsync_OffersFilledFieldsWholeWithTheirLabel()
    {
        const string Description = "Première ligne\nLien : https://exemple.fr\nContact : marie@exemple.fr";
        var reader = new FakeWindowTextReader
        {
            Windows = new[]
            {
                Window("Studio", rank: 1, string.Empty) with
                {
                    Fields = new[]
                    {
                        new FieldText { Label = " Title (required) ", Value = " Mon tuto PasteFlow " },
                        new FieldText { Label = "Description", Value = Description },
                    },
                },
            },
        };

        var candidates = await new ScreenCandidateSource(reader).GetCandidatesAsync(CancellationToken.None);

        Assert.Collection(
            candidates,
            candidate => Assert.Equal(
                new Candidate { Value = "Mon tuto PasteFlow", Label = "Title (required)", SourceName = "Studio", SourceRank = 1 },
                candidate),
            candidate => Assert.Equal(
                new Candidate { Value = Description, Label = "Description", SourceName = "Studio", SourceRank = 1 },
                candidate));
    }

    [Theory]
    [InlineData("marie@exemple.fr", CandidateKind.Email)]
    [InlineData("03 26 47 21 85", CandidateKind.Phone)]
    [InlineData("14/10/2026", CandidateKind.Date)]
    [InlineData("Marie", CandidateKind.Text)]
    public async Task GetCandidatesAsync_GivesFilledFieldsTheKindOfTheirWholeValue(string value, CandidateKind kind)
    {
        var reader = new FakeWindowTextReader
        {
            Windows = new[] { Window("Ancien formulaire", rank: 1, string.Empty) with { Fields = new[] { new FieldText { Label = "Champ", Value = value } } } },
        };

        var candidates = await new ScreenCandidateSource(reader).GetCandidatesAsync(CancellationToken.None);

        Assert.Equal(kind, Assert.Single(candidates).Kind);
    }

    [Theory]
    [InlineData("Rechercher", "chaussures rouges")]
    [InlineData("Address and search bar", "youtube.com")]
    [InlineData("Nom", "   ")]
    public async Task GetCandidatesAsync_SkipsSearchBoxesAndBlankFields(string label, string value)
    {
        var reader = new FakeWindowTextReader
        {
            Windows = new[] { Window("Chrome", rank: 1, string.Empty) with { Fields = new[] { new FieldText { Label = label, Value = value } } } },
        };

        var candidates = await new ScreenCandidateSource(reader).GetCandidatesAsync(CancellationToken.None);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task GetCandidatesAsync_WithoutReadableWindow_ReturnsNothing()
    {
        var source = new ScreenCandidateSource(new FakeWindowTextReader());

        var candidates = await source.GetCandidatesAsync(CancellationToken.None);

        Assert.Empty(candidates);
    }

    private static WindowText Window(string title, int rank, string text)
    {
        return new WindowText { Title = title, Rank = rank, Text = text };
    }
}
