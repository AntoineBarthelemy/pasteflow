using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Matching;

public sealed class FormMatcherTests
{
    [Fact]
    public void MatchAll_GivesEachValueToOneFieldOnly()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email"), TestFields.Text("sponsor", "Email du parrain") },
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        AssertFills(matches[0], "antoine.b@orange.fr");
        Assert.False(matches[1].IsConfident);
    }

    [Fact]
    public void MatchAll_GivesDisputedValueToSurestFieldWhateverTheOrderOfTheForm()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("sponsor", "Email du parrain"), TestFields.Text("email", "Email") },
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        Assert.False(matches[0].IsConfident);
        AssertFills(matches[1], "antoine.b@orange.fr");
    }

    [Fact]
    public void MatchAll_GivesDisputedValueToFirstFieldWhenBothAreAsSure()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("phone", "Téléphone"), TestFields.Text("mobile", "Mobile") },
            new[] { Candidate("06 47 21 85 39", CandidateKind.Phone, string.Empty) });

        AssertFills(matches[0], "06 47 21 85 39");
        Assert.False(matches[1].IsConfident);
    }

    [Fact]
    public void MatchAll_FieldLosingItsValueTakesAnotherOneThatFitsItOnItsOwn()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email"), TestFields.Text("sponsor", "Email du parrain") },
            new[]
            {
                Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email"),
                Candidate("lea.martin@free.fr", CandidateKind.Email, "Email du parrain"),
            });

        AssertFills(matches[0], "antoine.b@orange.fr");
        AssertFills(matches[1], "lea.martin@free.fr");
    }

    [Fact]
    public void MatchAll_NeverFillsAFieldByElimination()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email"), TestFields.Text("sponsor", "Email du parrain") },
            new[]
            {
                Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email"),
                Candidate("contact@reims-auto.fr", CandidateKind.Email, string.Empty),
            });

        AssertFills(matches[0], "antoine.b@orange.fr");
        Assert.False(matches[1].IsConfident);
        Assert.Equal("contact@reims-auto.fr", matches[1].Candidate?.Value);
    }

    [Theory]
    [InlineData("Confirmez votre email", "")]
    [InlineData("Confirmation de l'e-mail", "")]
    [InlineData("Répéter l'adresse mail", "")]
    [InlineData("Email (again)", "")]
    [InlineData("", "emailConfirm")]
    [InlineData("", "email_confirmation")]
    public void MatchAll_ConfirmationFieldReceivesTheSameValue(string label, string technicalName)
    {
        var confirmation = TestFields.Text("confirm", label) with { TechnicalName = technicalName };

        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email"), confirmation },
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        AssertFills(matches[0], "antoine.b@orange.fr");
        AssertFills(matches[1], "antoine.b@orange.fr");
    }

    [Fact]
    public void MatchAll_ConfirmationFieldDoesNotTakeTheValueFromTheFieldItConfirms()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("confirm", "Confirmez votre email"), TestFields.Text("email", "Email") },
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        AssertFills(matches[0], "antoine.b@orange.fr");
        AssertFills(matches[1], "antoine.b@orange.fr");
    }

    [Fact]
    public void MatchAll_UncertainFieldDoesNotKeepItsValueFromOtherFields()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("misc", "Commentaire"), TestFields.Text("email", "Email") },
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        AssertFills(matches[1], "antoine.b@orange.fr");
    }

    [Fact]
    public void MatchAll_ReturnsMatchesInTheOrderOfTheForm()
    {
        var fields = new[]
        {
            TestFields.Text("misc", "Commentaire"),
            TestFields.Text("phone", "Téléphone"),
            TestFields.Text("email", "Email"),
        };

        var matches = FormMatcher.MatchAll(
            fields,
            new[]
            {
                Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email"),
                Candidate("06 47 21 85 39", CandidateKind.Phone, string.Empty),
            });

        Assert.Equal(fields, matches.Select(match => match.Field));
    }

    [Fact]
    public void MatchAll_WhenTheFormAsksTheFirstNameApart_FillsFirstAndLastNames()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("first", "Prénom"), TestFields.Text("last", "Nom") },
            Person("Marie Lefèvre", "Marie", "Lefèvre"),
            TestReferenceData.Create());

        AssertFills(matches[0], "Marie");
        AssertFills(matches[1], "Lefèvre");
    }

    [Fact]
    public void MatchAll_WhenTheFormAsksOnlyTheName_FillsTheWholeName()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("name", "Nom"), TestFields.Text("email", "Email") },
            Person("Marie Lefèvre", "Marie", "Lefèvre"),
            TestReferenceData.Create());

        AssertFills(matches[0], "Marie Lefèvre");
    }

    [Fact]
    public void MatchAll_WithTwoPeopleOnScreen_LeavesNamesToCheckWithBothProposed()
    {
        var candidates = Person("Marie Lefèvre", "Marie", "Lefèvre").Concat(Person("Antoine Barthélémy", "Antoine", "Barthélémy")).ToList();

        var matches = FormMatcher.MatchAll(new[] { TestFields.Text("first", "Prénom") }, candidates, TestReferenceData.Create());

        Assert.False(matches[0].IsConfident);
        Assert.Equal(new[] { "Marie", "Antoine" }, matches[0].Suggestions.Select(candidate => candidate.Value));
    }

    [Fact]
    public void MatchAll_WhenTheFirstNameFieldIsAlreadyFilled_NomStillMeansTheLastName()
    {
        var form = new[] { TestFields.Text("first", "Prénom", hasValue: true), TestFields.Text("last", "Nom") };

        var matches = FormMatcher.MatchAll(
            new[] { form[1] },
            Person("Antoine Barthélémy", "Antoine", "Barthélémy"),
            TestReferenceData.Create(),
            MatchingOptions.Balanced,
            form);

        AssertFills(matches[0], "Barthélémy");
    }

    [Theory]
    [InlineData("Wei Zhang", "Nom")]
    [InlineData("Rose Dupont", "Nom")]
    public void MatchAll_ALastNameFieldNeverTakesAWholeNameReadAsText(string value, string label)
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("first", "Prénom"), TestFields.Text("last", "Nom") },
            new[] { Candidate(value, CandidateKind.Text, label) },
            TestReferenceData.Create());

        Assert.Null(matches[1].Candidate);
    }

    [Fact]
    public void MatchAll_ALastNameFieldTakesSeveralWordsLabelledAsAFamilyName()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("first", "Prénom"), TestFields.Text("last", "Nom de famille") },
            new[] { Candidate("De La Fontaine", CandidateKind.Text, "Nom de famille") },
            TestReferenceData.Create());

        AssertFills(matches[1], "De La Fontaine");
    }

    [Fact]
    public void MatchAll_FillsCommuneAndPostalCodeThatGoTogether()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("postal", "Code postal"), TestFields.Text("city", "Ville") },
            new[] { Candidate("51100", CandidateKind.Number, string.Empty), Candidate("Reims", CandidateKind.City, string.Empty) },
            TestReferenceData.Create());

        AssertFills(matches[0], "51100");
        AssertFills(matches[1], "Reims");
    }

    [Fact]
    public void MatchAll_LeavesCommuneAndPostalCodeThatDoNotGoTogetherToCheck()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("postal", "Code postal"), TestFields.Text("city", "Ville") },
            new[] { Candidate("51500", CandidateKind.Number, string.Empty), Candidate("Reims", CandidateKind.City, string.Empty) },
            TestReferenceData.Create());

        Assert.False(matches[0].IsConfident);
        Assert.False(matches[1].IsConfident);
        Assert.Equal("51500", Assert.Single(matches[0].Suggestions).Value);
        Assert.Equal("Reims", Assert.Single(matches[1].Suggestions).Value);
    }

    [Fact]
    public void MatchAll_DoesNotJudgeACommuneTheListsDoNotKnow()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("postal", "Code postal"), TestFields.Text("city", "Ville") },
            new[] { Candidate("51500", CandidateKind.Number, string.Empty), Candidate("Bezannes", CandidateKind.Text, "Ville") },
            TestReferenceData.Create());

        AssertFills(matches[0], "51500");
        AssertFills(matches[1], "Bezannes");
    }

    [Fact]
    public void MatchAll_FillsFromTheHistoryTheFieldsTheScreenSaysNothingAbout()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email"), TestFields.Text("phone", "Téléphone") },
            new[] { Candidate("06 47 21 85 39", CandidateKind.Phone, "Tél."), FromHistory("antoine.b@orange.fr", CandidateKind.Email, "Email") },
            TestReferenceData.Create());

        AssertFills(matches[0], "antoine.b@orange.fr");
        Assert.True(matches[0].Candidate!.IsFromHistory);
        AssertFills(matches[1], "06 47 21 85 39");
    }

    [Fact]
    public void MatchAll_NeverPrefersTheHistoryToASureValueOnScreen()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email") },
            new[] { FromHistory("antoine.b@orange.fr", CandidateKind.Email, "Email"), Candidate("contact@reims-auto.fr", CandidateKind.Email, string.Empty) },
            TestReferenceData.Create());

        AssertFills(matches[0], "contact@reims-auto.fr");
    }

    [Fact]
    public void MatchAll_WhenTheScreenHesitates_TheHistoryOnlyProposes()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email") },
            new[]
            {
                Candidate("contact@reims-auto.fr", CandidateKind.Email, string.Empty),
                Candidate("lea.martin@free.fr", CandidateKind.Email, string.Empty),
                FromHistory("antoine.b@orange.fr", CandidateKind.Email, "Email"),
            },
            TestReferenceData.Create());

        Assert.False(matches[0].IsConfident);
        Assert.Equal(
            new[] { "contact@reims-auto.fr", "lea.martin@free.fr", "antoine.b@orange.fr" },
            matches[0].Suggestions.Select(candidate => candidate.Value));
    }

    [Fact]
    public void MatchAll_TheHistoryDoesNotGiveAValueTakenOnScreenToAnotherField()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email"), TestFields.Text("sponsor", "Email du parrain") },
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email"), FromHistory("antoine.b@orange.fr", CandidateKind.Email, "Email du parrain") },
            TestReferenceData.Create());

        AssertFills(matches[0], "antoine.b@orange.fr");
        Assert.False(matches[1].IsConfident);
    }

    [Fact]
    public void MatchAll_WithTwoValuesInTheHistory_LetsTheUserChoose()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email") },
            new[] { FromHistory("antoine.b@orange.fr", CandidateKind.Email, "Email"), FromHistory("antoine@codilee.fr", CandidateKind.Email, "Email") },
            TestReferenceData.Create());

        Assert.False(matches[0].IsConfident);
        Assert.Equal(2, matches[0].Suggestions.Count);
    }

    [Fact]
    public void MatchAll_WithoutFields_ReturnsNothing()
    {
        var matches = FormMatcher.MatchAll(
            Array.Empty<FormField>(),
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        Assert.Empty(matches);
    }

    private static Candidate FromHistory(string value, CandidateKind kind, string label)
    {
        return Candidate(value, kind, label) with { SourceName = "Historique", SourceRank = 0, IsFromHistory = true };
    }

    private static Candidate[] Person(string fullName, string firstName, string lastName)
    {
        return new[]
        {
            Candidate(fullName, CandidateKind.FullName, string.Empty),
            Candidate(firstName, CandidateKind.FirstName, string.Empty),
            Candidate(lastName, CandidateKind.LastName, string.Empty),
        };
    }

    private static Candidate Candidate(string value, CandidateKind kind, string label)
    {
        return new Candidate { Value = value, Kind = kind, Label = label, SourceName = "Gmail", SourceRank = 1 };
    }

    private static void AssertFills(FieldMatch match, string expectedValue)
    {
        Assert.True(match.IsConfident, $"Not confident, score {match.Score}");
        Assert.Equal(expectedValue, match.Candidate?.Value);
    }
}
