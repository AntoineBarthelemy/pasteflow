using PasteFlow.Core.Candidates;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Candidates;

public sealed class ScreenCandidateSourceTests
{
    [Fact]
    public async Task GetCandidatesAsync_OffersEachLineWithItsWindow()
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
            candidate => Assert.Equal("Commande n° CMD-48213", candidate.Value));
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
