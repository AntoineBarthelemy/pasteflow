using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Tests.Candidates;

public sealed class TextSegmenterTests
{
    [Theory]
    [InlineData("Commande n° CMD-48213", "CMD-48213", CandidateKind.Reference, "Commande n°")]
    [InlineData("Email : antoine@example.com", "antoine@example.com", CandidateKind.Email, "Email")]
    [InlineData("Tél. 06 12 34 56 78", "06 12 34 56 78", CandidateKind.Phone, "Tél.")]
    [InlineData("Portable : 0612345678", "0612345678", CandidateKind.Phone, "Portable")]
    [InlineData("+33 6 12 34 56 78", "+33 6 12 34 56 78", CandidateKind.Phone, "")]
    [InlineData("Livraison prévue le 12/10/2026", "12/10/2026", CandidateKind.Date, "Livraison prévue le")]
    [InlineData("Date : 2026-10-05", "2026-10-05", CandidateKind.Date, "Date")]
    [InlineData("Rendez-vous à 14h30 le 05.10.26", "05.10.26", CandidateKind.Date, "Rendez-vous à 14h30 le")]
    [InlineData("Total : 49,90 €", "49,90 €", CandidateKind.Amount, "Total")]
    [InlineData("Montant 1 250,00 €", "1 250,00 €", CandidateKind.Amount, "Montant")]
    [InlineData("Prix : $19.99", "$19.99", CandidateKind.Amount, "Prix")]
    [InlineData("IBAN : FR76 3000 6000 0112 3456 7890 189", "FR76 3000 6000 0112 3456 7890 189", CandidateKind.Iban, "IBAN")]
    [InlineData("Site : https://antoinebarthelemy.dev.", "https://antoinebarthelemy.dev", CandidateKind.Url, "Site")]
    [InlineData("Voir www.example.com", "www.example.com", CandidateKind.Url, "Voir")]
    [InlineData("Numéro client 4521873", "4521873", CandidateKind.Number, "Numéro client")]
    public void Segment_RecognizesValueWithItsLabel(string line, string value, CandidateKind kind, string label)
    {
        var pieces = TextSegmenter.Segment(line);

        Assert.Contains(Piece(value, kind, label), pieces);
    }

    [Fact]
    public void Segment_KeepsWholeLineNextToRecognizedValues()
    {
        var pieces = TextSegmenter.Segment("51500 Taissy");

        Assert.Equal(new[] { Piece("51500", CandidateKind.Number, string.Empty), Piece("51500 Taissy", CandidateKind.Text, string.Empty) }, pieces);
    }

    [Fact]
    public void Segment_DoesNotDuplicateLineMadeOfOneValue()
    {
        var pieces = TextSegmenter.Segment("antoine@example.com");

        Assert.Equal(new[] { Piece("antoine@example.com", CandidateKind.Email, string.Empty) }, pieces);
    }

    [Fact]
    public void Segment_FindsSeveralValuesOnOneLineEachWithItsOwnLabel()
    {
        var pieces = TextSegmenter.Segment("Contact: antoine@example.com ou 06 12 34 56 78");

        Assert.Contains(Piece("antoine@example.com", CandidateKind.Email, "Contact"), pieces);
        Assert.Contains(Piece("06 12 34 56 78", CandidateKind.Phone, "ou"), pieces);
    }

    [Fact]
    public void Segment_SplitsLabelAndValueOfPlainText()
    {
        var pieces = TextSegmenter.Segment("Nom : Antoine Barthélémy");

        Assert.Equal(new[] { Piece("Antoine Barthélémy", CandidateKind.Text, "Nom") }, pieces);
    }

    [Fact]
    public void Segment_UsesLabelOnPreviousLine()
    {
        var pieces = TextSegmenter.Segment("Adresse :\n12 rue des Lilas\nVille : Reims");

        Assert.Contains(Piece("12 rue des Lilas", CandidateKind.Text, "Adresse"), pieces);
        Assert.DoesNotContain(pieces, piece => piece.Value == "Adresse :");
    }

    [Fact]
    public void Segment_LabelOnPreviousLineAppliesOnlyToNextLine()
    {
        var pieces = TextSegmenter.Segment("Email :\nantoine@example.com\n06 12 34 56 78");

        Assert.Contains(Piece("antoine@example.com", CandidateKind.Email, "Email"), pieces);
        Assert.Contains(Piece("06 12 34 56 78", CandidateKind.Phone, string.Empty), pieces);
    }

    [Theory]
    [InlineData("12:30")]
    [InlineData("Bonjour Antoine,")]
    [InlineData("code promo abc123 et A4")]
    public void Segment_KeepsLinesWithoutRecognizedValueAsText(string line)
    {
        var pieces = TextSegmenter.Segment(line);

        Assert.Equal(new[] { Piece(line, CandidateKind.Text, string.Empty) }, pieces);
    }

    [Fact]
    public void Segment_SplitsLinesWhateverTheLineBreaks()
    {
        var pieces = TextSegmenter.Segment("Bonjour\r\nMerci\rÀ bientôt");

        Assert.Equal(new[] { "Bonjour", "Merci", "À bientôt" }, pieces.Select(piece => piece.Value));
    }

    [Fact]
    public void Segment_TrimsAndSkipsEmptyLines()
    {
        var pieces = TextSegmenter.Segment("  Bonjour  \n\n   \n\tMerci\t");

        Assert.Equal(new[] { "Bonjour", "Merci" }, pieces.Select(piece => piece.Value));
    }

    [Fact]
    public void Segment_KeepsFirstOccurrenceCompletedByFirstLabelFound()
    {
        var pieces = TextSegmenter.Segment("antoine@example.com\nBonjour\nEmail : antoine@example.com");

        Assert.Equal(Piece("antoine@example.com", CandidateKind.Email, "Email"), pieces[0]);
        Assert.Single(pieces, piece => piece.Value == "antoine@example.com");
    }

    [Fact]
    public void Segment_SkipsParagraphsButKeepsTheirValues()
    {
        var paragraph = $"{new string('x', TextSegmenter.MaxPieceLength)} écrivez à antoine@example.com";

        var pieces = TextSegmenter.Segment($"Nom\n{paragraph}\nVille");

        Assert.Equal(new[] { "Nom", "antoine@example.com", "Ville" }, pieces.Select(piece => piece.Value));
    }

    [Fact]
    public void Segment_ShortensLongLabelsToTheirLastWords()
    {
        var pieces = TextSegmenter.Segment(
            "Merci pour votre confiance, voici le numéro de votre commande CMD-48213");

        var reference = Assert.Single(pieces, piece => piece.Kind == CandidateKind.Reference);
        Assert.True(reference.Label.Length <= TextSegmenter.MaxLabelLength);
        Assert.EndsWith("de votre commande", reference.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void Segment_DoesNotTakeIbanWithWrongKeyForAnIban()
    {
        var pieces = TextSegmenter.Segment("IBAN : FR76 3000 6000 0112 3456 7890 188");

        Assert.DoesNotContain(pieces, piece => piece.Kind == CandidateKind.Iban);
    }

    [Fact]
    public void Segment_DoesNotTakeImpossibleDateForADate()
    {
        var pieces = TextSegmenter.Segment("Livraison le 31/02/2026");

        Assert.DoesNotContain(pieces, piece => piece.Kind == CandidateKind.Date);
    }

    [Fact]
    public void Segment_DoesNotTakeMalformedEmailForAnEmail()
    {
        var pieces = TextSegmenter.Segment("Contact : antoine..b@orange.fr");

        Assert.DoesNotContain(pieces, piece => piece.Kind == CandidateKind.Email);
    }

    [Fact]
    public void Segment_WithEmptyText_ReturnsNothing()
    {
        Assert.Empty(TextSegmenter.Segment(string.Empty));
    }

    private static TextPiece Piece(string value, CandidateKind kind, string label)
    {
        return new TextPiece { Value = value, Kind = kind, Label = label };
    }
}
