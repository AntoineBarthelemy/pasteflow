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
    public void MatchAll_WithoutFields_ReturnsNothing()
    {
        var matches = FormMatcher.MatchAll(
            Array.Empty<FormField>(),
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        Assert.Empty(matches);
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
