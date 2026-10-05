using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Tests.Candidates;

public sealed class TextSegmenterTests
{
    [Fact]
    public void Segment_SplitsLinesWhateverTheLineBreaks()
    {
        var pieces = TextSegmenter.Segment("Bonjour\r\nCommande n° CMD-48213\nTotal : 49,90 €\rMerci");

        Assert.Equal(new[] { "Bonjour", "Commande n° CMD-48213", "Total : 49,90 €", "Merci" }, pieces);
    }

    [Fact]
    public void Segment_TrimsAndSkipsEmptyLines()
    {
        var pieces = TextSegmenter.Segment("  Bonjour  \n\n   \n\tMerci\t");

        Assert.Equal(new[] { "Bonjour", "Merci" }, pieces);
    }

    [Fact]
    public void Segment_KeepsFirstOccurrenceOfDuplicates()
    {
        var pieces = TextSegmenter.Segment("A\nB\nA\nC\nB");

        Assert.Equal(new[] { "A", "B", "C" }, pieces);
    }

    [Fact]
    public void Segment_SkipsParagraphsTooLongToBeValues()
    {
        var paragraph = new string('x', TextSegmenter.MaxPieceLength + 1);

        var pieces = TextSegmenter.Segment($"Nom\n{paragraph}\nVille");

        Assert.Equal(new[] { "Nom", "Ville" }, pieces);
    }

    [Fact]
    public void Segment_WithEmptyText_ReturnsNothing()
    {
        Assert.Empty(TextSegmenter.Segment(string.Empty));
    }
}
