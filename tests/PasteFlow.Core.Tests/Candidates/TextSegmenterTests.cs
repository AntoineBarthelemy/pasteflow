using PasteFlow.Core.Candidates;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Tests.Fakes;

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
        var pieces = TextSegmenter.Segment("51500 Bezannes", ReferenceData.Empty);

        Assert.Equal(new[] { Piece("51500", CandidateKind.Number, string.Empty), Piece("51500 Bezannes", CandidateKind.Text, string.Empty) }, pieces);
    }

    [Fact]
    public void Segment_CompanyNumbersWrittenByGroups_AreReadAsNumbersWithoutSpaces()
    {
        var pieces = TextSegmenter.Segment("SIREN 812 345 676 - SIRET 812 345 676 00017", ReferenceData.Empty);

        Assert.Contains(Piece("812345676", CandidateKind.Number, "SIREN"), pieces);
        Assert.Contains(Piece("81234567600017", CandidateKind.Number, "SIRET"), pieces);
    }

    [Fact]
    public void Segment_SocialSecurityNumberByGroups_IsReadWholeAndItsKeyApart()
    {
        var pieces = TextSegmenter.Segment("N° de sécurité sociale : 2 85 05 51 454 123 95", ReferenceData.Empty);

        Assert.Contains(Piece("285055145412395", CandidateKind.Number, "N° de sécurité sociale"), pieces);
        Assert.Contains(Piece("95", CandidateKind.Text, "Clé"), pieces);
    }

    [Fact]
    public void Segment_CarPlate_IsWrittenAsTheRegistrationCertificateShowsIt()
    {
        var pieces = TextSegmenter.Segment("Immatriculation GH 482 KT", ReferenceData.Empty);

        Assert.Contains(pieces, piece => piece is { Value: "GH-482-KT", Kind: CandidateKind.Reference });
    }

    [Fact]
    public void Segment_RoleBeforeAPerson_LabelsThePersonAndEachOfItsValues()
    {
        var pieces = TextSegmenter.Segment("Locataire : Mme Sophie Garnier, sophie.garnier@garnier-famille.fr, 06 52 74 18 39");

        Assert.Contains(Piece("Sophie Garnier", CandidateKind.FullName, "Locataire"), pieces);
        Assert.Contains(Piece("sophie.garnier@garnier-famille.fr", CandidateKind.Email, "Locataire : Mme Sophie Garnier"), pieces);
        Assert.Contains(Piece("06 52 74 18 39", CandidateKind.Phone, "Locataire"), pieces);
    }

    [Fact]
    public void Segment_RoleBeforeAPersonAndItsAddress_GivesTheAddressApart()
    {
        var pieces = TextSegmenter.Segment("Débiteur : M. Julien Masson, 22 avenue de Laon, 51100 Reims");

        Assert.Contains(Piece("Julien Masson", CandidateKind.FullName, "Débiteur"), pieces);
        Assert.Contains(Piece("22 avenue de Laon, 51100 Reims", CandidateKind.Text, "Débiteur adresse"), pieces);
    }

    [Fact]
    public void Segment_LabelBeforeAValueOnly_IsNotARole()
    {
        Assert.Contains(Piece("49,90 €", CandidateKind.Amount, "Total TTC"), TextSegmenter.Segment("Total TTC : 49,90 €", ReferenceData.Empty));
    }

    [Fact]
    public void Segment_NumberByGroupsWithAWrongKey_IsNotACompanyNumber()
    {
        var pieces = TextSegmenter.Segment("Réf. 123 456 789", ReferenceData.Empty);

        Assert.DoesNotContain(pieces, piece => piece.Value == "123456789");
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
        var pieces = TextSegmenter.Segment("Commentaire : livrer après 18h", ReferenceData.Empty);

        Assert.Equal(new[] { Piece("livrer après 18h", CandidateKind.Text, "Commentaire") }, pieces);
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
    public void Segment_ReadsPersonGivenAsLabelAndValue()
    {
        var pieces = TextSegmenter.Segment("Nom : Antoine Barthélémy", TestReferenceData.Create());

        Assert.Equal(
            new[]
            {
                Piece("Antoine Barthélémy", CandidateKind.FullName, "Nom"),
                Piece("Antoine", CandidateKind.FirstName, "Nom"),
                Piece("Barthélémy", CandidateKind.LastName, "Nom"),
            },
            pieces);
    }

    [Fact]
    public void Segment_ReadsPersonBeforeTheirEmail()
    {
        var pieces = TextSegmenter.Segment("Antoine Barthélémy <antoine.b@orange.fr>", TestReferenceData.Create());

        Assert.Contains(Piece("Antoine Barthélémy", CandidateKind.FullName, string.Empty), pieces);
        Assert.Contains(Piece("antoine.b@orange.fr", CandidateKind.Email, "Antoine Barthélémy"), pieces);
    }

    [Fact]
    public void Segment_ReadsCommuneNextToItsPostalCode()
    {
        var pieces = TextSegmenter.Segment("51500 Taissy", TestReferenceData.Create());

        Assert.Equal(
            new[]
            {
                Piece("51500", CandidateKind.Number, string.Empty),
                Piece("Taissy", CandidateKind.City, string.Empty),
                Piece("51500 Taissy", CandidateKind.Text, string.Empty),
            },
            pieces);
    }

    [Fact]
    public void Segment_ReadsAWholeSignature()
    {
        var pieces = TextSegmenter.Segment(
            "Cordialement,\nMarie Lefèvre\nResponsable achats\nBoulangerie Martin SARL\n14 avenue Jean Jaurès\n51100 Reims\nFrance",
            TestReferenceData.Create());

        Assert.Contains(Piece("Marie Lefèvre", CandidateKind.FullName, string.Empty), pieces);
        Assert.Contains(Piece("Marie", CandidateKind.FirstName, string.Empty), pieces);
        Assert.Contains(Piece("Lefèvre", CandidateKind.LastName, string.Empty), pieces);
        Assert.Contains(Piece("Boulangerie Martin SARL", CandidateKind.Company, string.Empty), pieces);
        Assert.Contains(Piece("Reims", CandidateKind.City, string.Empty), pieces);
        Assert.Contains(Piece("France", CandidateKind.Country, string.Empty), pieces);
        Assert.DoesNotContain(pieces, piece => piece.Kind == CandidateKind.FullName && piece.Value != "Marie Lefèvre");
    }

    [Fact]
    public void Segment_WithoutOfficialLists_RecognizesOnlyShapes()
    {
        var pieces = TextSegmenter.Segment("Marie Lefèvre\n51100 Reims", ReferenceData.Empty);

        Assert.DoesNotContain(pieces, piece => piece.Kind is CandidateKind.FullName or CandidateKind.City);
    }

    [Theory]
    [InlineData("Prénom : France")]
    [InlineData("Prénom :\nFrance")]
    public void Segment_KeepsWhatTheLabelSaysOverWhatTheListsRecognize(string text)
    {
        var pieces = TextSegmenter.Segment(text, TestReferenceData.Create());

        Assert.Equal(new[] { Piece("France", CandidateKind.Text, "Prénom") }, pieces);
    }

    [Fact]
    public void Segment_KeepsTheCountryWhenTheLabelAsksForIt()
    {
        var pieces = TextSegmenter.Segment("Pays : France", TestReferenceData.Create());

        Assert.Contains(Piece("France", CandidateKind.Country, "Pays"), pieces);
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
