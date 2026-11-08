using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;

namespace PasteFlow.Core.Tests.Matching;

public sealed class ChoiceMatcherTests
{
    [Fact]
    public void Match_OptionWrittenInAValueAboutTheField_IsChosen()
    {
        var field = Choice("Type de logement", "Maison", "Appartement");

        var match = ChoiceMatcher.Match(field, new[] { Candidate("appartement de 3 pièces, 64 m², 17 rue Chanzy", "Logement") });

        Assert.True(match.IsConfident);
        Assert.Equal("Appartement", match.Candidate?.Value);
    }

    [Fact]
    public void Match_CountryOfTheExpectedKind_IsChosenWithoutLabel()
    {
        var field = Choice("Pays de livraison", "France", "Belgique", "Suisse");

        var match = ChoiceMatcher.Match(field, new[] { Candidate("Belgique", string.Empty) with { Kind = CandidateKind.Country } });

        Assert.True(match.IsConfident);
        Assert.Equal("Belgique", match.Candidate?.Value);
    }

    [Fact]
    public void Match_ListWithoutKnownOptions_TakesTheOnlyValueOfTheExpectedKind()
    {
        var field = Choice("Pays de livraison");

        var match = ChoiceMatcher.Match(field, new[] { Candidate("Belgique", "Adresse de livraison") with { Kind = CandidateKind.Country } });

        Assert.True(match.IsConfident);
        Assert.Equal("Belgique", match.Candidate?.Value);
    }

    [Fact]
    public void Match_ListWithoutKnownOptions_FindsTheCountryInsideAnAddress()
    {
        var field = Choice("Pays de livraison");

        var match = ChoiceMatcher.Match(field, new[] { Candidate("Rue Haute 41, 1000 Bruxelles, Belgique", "Adresse de livraison") });

        Assert.True(match.IsConfident);
        Assert.Equal("Belgique", match.Candidate?.Value);
    }

    [Fact]
    public void Match_TwoOptionsWritten_IsNotSure()
    {
        var field = Choice("Type de logement", "Maison", "Appartement");

        var match = ChoiceMatcher.Match(field, new[] { Candidate("maison ou appartement, au choix", "Logement") });

        Assert.False(match.IsConfident);
        Assert.Equal(2, match.Suggestions.Count);
    }

    [Fact]
    public void Match_OptionInAValueAboutSomethingElse_IsNotChosen()
    {
        var field = Choice("Type de logement", "Maison", "Appartement");

        var match = ChoiceMatcher.Match(field, new[] { Candidate("Maison Martin, 3 rue des Vignes", "Expéditeur") });

        Assert.Null(match.Candidate);
    }

    [Theory]
    [InlineData("oui", true)]
    [InlineData("non", false)]
    [InlineData("appartement non meublé", false)]
    public void Match_TickBox_IsTickedOnlyOnAClearYes(string value, bool isTicked)
    {
        var field = new FormField { Id = "meuble", Label = "Logement meublé", Kind = FieldKind.CheckBox, HasValue = false };

        var match = ChoiceMatcher.Match(field, new[] { Candidate(value, "Meublé") });

        Assert.Equal(isTicked, match.IsConfident);
        Assert.Equal(isTicked ? ChoiceMatcher.Ticked : null, match.Candidate?.Value);
    }

    private static FormField Choice(string label, params string[] options)
    {
        return new FormField { Id = "choice", Label = label, Kind = FieldKind.Choice, HasValue = false, Options = options };
    }

    private static Candidate Candidate(string value, string label)
    {
        return new Candidate { Value = value, Kind = CandidateKind.Text, Label = label, SourceName = "Mail", SourceRank = 1 };
    }
}
