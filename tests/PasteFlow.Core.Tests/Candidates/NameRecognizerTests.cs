using PasteFlow.Core.Candidates;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Candidates;

public sealed class NameRecognizerTests
{
    private readonly ReferenceData _data = TestReferenceData.Create();

    [Theory]
    [InlineData("Antoine Barthélémy", "Antoine Barthélémy", "Antoine", "Barthélémy")]
    [InlineData("BARTHÉLÉMY Antoine", "BARTHÉLÉMY Antoine", "Antoine", "BARTHÉLÉMY")]
    [InlineData("ANTOINE BARTHELEMY", "ANTOINE BARTHELEMY", "ANTOINE", "BARTHELEMY")]
    [InlineData("Jean Pierre Dupont", "Jean Pierre Dupont", "Jean Pierre", "Dupont")]
    [InlineData("Jean-Pierre Dupont", "Jean-Pierre Dupont", "Jean-Pierre", "Dupont")]
    [InlineData("Charles de Gaulle", "Charles de Gaulle", "Charles", "de Gaulle")]
    [InlineData("DE LA FONTAINE Jean", "DE LA FONTAINE Jean", "Jean", "DE LA FONTAINE")]
    [InlineData("M. Antoine Barthélémy", "Antoine Barthélémy", "Antoine", "Barthélémy")]
    [InlineData("Marie Lefèvre,", "Marie Lefèvre", "Marie", "Lefèvre")]
    [InlineData("Pierre Martin", "Pierre Martin", "Pierre", "Martin")]
    [InlineData("MARTIN Pierre", "MARTIN Pierre", "Pierre", "MARTIN")]
    [InlineData("Marie d'Arcy", "Marie d'Arcy", "Marie", "d'Arcy")]
    [InlineData("MARTIN Jean Pierre", "MARTIN Jean Pierre", "Jean Pierre", "MARTIN")]
    public void RecognizeSpan_ReadsPeople(string span, string fullName, string firstName, string lastName)
    {
        var pieces = NameRecognizer.RecognizeSpan(span, "De", _data);

        Assert.Equal(
            new[]
            {
                Piece(fullName, CandidateKind.FullName, "De"),
                Piece(firstName, CandidateKind.FirstName, "De"),
                Piece(lastName, CandidateKind.LastName, "De"),
            },
            pieces);
    }

    [Theory]
    [InlineData("Bonjour Antoine")]
    [InlineData("Antoine")]
    [InlineData("antoine barthélémy")]
    [InlineData("Antoine Barthélémy 06")]
    [InlineData("Antoine Jean Paul Pierre Dupont")]
    [InlineData("Antoine Garage")]
    [InlineData("Lycée Jean Moulin")]
    [InlineData("Barthélémy Antoine")]
    [InlineData("Antoine de")]
    public void RecognizeSpan_LeavesWhatIsNotSurelyAPerson(string span)
    {
        Assert.Empty(NameRecognizer.RecognizeSpan(span, string.Empty, _data));
    }

    [Theory]
    [InlineData("Rose Dupont", false)]
    [InlineData("Mme Rose Dupont", true)]
    [InlineData("Rose DUPONT", true)]
    [InlineData("France Télévisions", false)]
    public void RecognizeSpan_FirstNamesThatAreAlsoWordsNeedMoreEvidence(string span, bool isPerson)
    {
        var pieces = NameRecognizer.RecognizeSpan(span, string.Empty, _data);

        Assert.Equal(isPerson, pieces.Any(piece => piece.Kind == CandidateKind.FullName));
    }

    [Theory]
    [InlineData("France")]
    [InlineData("BELGIQUE")]
    [InlineData("Germany.")]
    public void RecognizeSpan_ReadsCountriesBeforeFirstNames(string span)
    {
        var piece = Assert.Single(NameRecognizer.RecognizeSpan(span, "Pays", _data));

        Assert.Equal(CandidateKind.Country, piece.Kind);
        Assert.Equal(span.TrimEnd('.'), piece.Value);
    }

    [Theory]
    [InlineData("Reims Auto SARL", "Reims Auto SARL")]
    [InlineData("SARL Reims Auto", "SARL Reims Auto")]
    [InlineData("Acme Inc.", "Acme Inc")]
    [InlineData("S.A.R.L. Dupont", "S.A.R.L. Dupont")]
    [InlineData("Boulangerie Martin SAS", "Boulangerie Martin SAS")]
    public void RecognizeSpan_ReadsCompaniesThroughTheirLegalForm(string span, string expected)
    {
        var piece = Assert.Single(NameRecognizer.RecognizeSpan(span, string.Empty, _data));

        Assert.Equal(CandidateKind.Company, piece.Kind);
        Assert.Equal(expected, piece.Value);
    }

    [Theory]
    [InlineData("la sa voiture")]
    [InlineData("SAS au capital de 10 000 €")]
    [InlineData("SARL")]
    public void RecognizeSpan_DoesNotTakeAnyMentionOfALegalFormForACompany(string span)
    {
        Assert.DoesNotContain(NameRecognizer.RecognizeSpan(span, string.Empty, _data), piece => piece.Kind == CandidateKind.Company);
    }

    [Theory]
    [InlineData("Reims, le 12 octobre 2026", "Reims")]
    [InlineData("Châlons-en-Champagne, le 3 mars", "Châlons-en-Champagne")]
    public void RecognizeSpan_ReadsCommunesStartingADatedLine(string span, string expected)
    {
        var piece = Assert.Single(NameRecognizer.RecognizeSpan(span, string.Empty, _data));

        Assert.Equal(CandidateKind.City, piece.Kind);
        Assert.Equal(expected, piece.Value);
    }

    [Theory]
    [InlineData("Reims")]
    [InlineData("REIMS")]
    [InlineData("VILLE")]
    [InlineData("RUE")]
    [InlineData("ORANGE")]
    [InlineData("Bonjour, le colis est arrivé")]
    public void RecognizeSpan_DoesNotTakeWordsForCommunes(string span)
    {
        Assert.Empty(NameRecognizer.RecognizeSpan(span, string.Empty, _data));
    }

    [Theory]
    [InlineData("", "51500", " Taissy", "Taissy")]
    [InlineData("", "51100", " REIMS CEDEX 2", "REIMS")]
    [InlineData("", "85160", " Saint-Jean-de-Monts, France", "Saint-Jean-de-Monts")]
    [InlineData("12 rue des Lilas, Taissy ", "51500", "", "Taissy")]
    public void CommuneNextToPostalCode_FindsTheCommuneOfThePostalCode(string before, string postalCode, string after, string expected)
    {
        Assert.Equal(expected, NameRecognizer.CommuneNextToPostalCode(before, postalCode, after, _data));
    }

    [Theory]
    [InlineData("", "51500", " Reims")]
    [InlineData("", "51500", "")]
    [InlineData("Commande ", "51500", " livrée")]
    public void CommuneNextToPostalCode_WhenNoCommuneGoesWithIt_ReturnsNull(string before, string postalCode, string after)
    {
        Assert.Null(NameRecognizer.CommuneNextToPostalCode(before, postalCode, after, _data));
    }

    private static TextPiece Piece(string value, CandidateKind kind, string label)
    {
        return new TextPiece { Value = value, Kind = kind, Label = label };
    }
}
