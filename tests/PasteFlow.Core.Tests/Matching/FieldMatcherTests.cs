using PasteFlow.Core.Candidates;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Matching;

public sealed class FieldMatcherTests
{
    [Fact]
    public void Match_ValueOfExpectedKindWithSameLabel_IsConfident()
    {
        var match = Match("Email", Candidate("antoine@example.com", CandidateKind.Email, "Email"));

        AssertFills(match, "antoine@example.com");
    }

    [Fact]
    public void Match_LabelsWrittenDifferently_AreRecognized()
    {
        var match = Match(
            "Référence",
            Candidate("CMD-48213", CandidateKind.Reference, "Commande n°"),
            Candidate("Bonjour Antoine,", CandidateKind.Text, string.Empty));

        AssertFills(match, "CMD-48213");
    }

    [Fact]
    public void Match_OnlyValueOfExpectedKindOnScreen_IsConfidentEvenWithoutLabel()
    {
        var match = Match("Téléphone", Candidate("06 12 34 56 78", CandidateKind.Phone, string.Empty));

        AssertFills(match, "06 12 34 56 78");
    }

    [Fact]
    public void Match_TwoValuesOfExpectedKindWithoutLabel_IsNotConfident()
    {
        var match = Match(
            "Email",
            Candidate("antoine@example.com", CandidateKind.Email, string.Empty),
            Candidate("contact@example.com", CandidateKind.Email, string.Empty));

        Assert.False(match.IsConfident);
        Assert.Equal("Deux valeurs aussi probables : « antoine@example.com » et « contact@example.com ».", match.Reason);
    }

    [Fact]
    public void Match_TheAddressBehindALink_OnlyGoesWhereALinkIsExpected()
    {
        var link = Candidate("https://www.youtube.com/watch?v=abc123", CandidateKind.Url, "Ma vidéo de rentrée") with { IsLinkAddress = true };

        Assert.Null(Match("Commentaire", link).Candidate);
        Assert.Equal(link.Value, Assert.Single(Match("Lien", link).Suggestions).Value);
    }

    [Fact]
    public void Match_TheAddressBehindALink_IsSureWhenItsTextTalksAboutTheField()
    {
        var match = Match(
            "Lien de la vidéo",
            Candidate("https://www.youtube.com/watch?v=abc123", CandidateKind.Url, "Ma vidéo de rentrée") with { IsLinkAddress = true },
            Candidate("https://www.youtube.com/@successstory", CandidateKind.Url, "Success Story") with { IsLinkAddress = true });

        AssertFills(match, "https://www.youtube.com/watch?v=abc123");
    }

    [Fact]
    public void Match_TheAddressBehindALink_NeverMakesAWrittenAddressLessSure()
    {
        var match = Match(
            "Site web",
            Candidate("https://garage-martin.fr", CandidateKind.Url, string.Empty),
            Candidate("https://www.facebook.com/garage", CandidateKind.Url, "Facebook") with { IsLinkAddress = true });

        AssertFills(match, "https://garage-martin.fr");
    }

    [Fact]
    public void Match_NoValue_SaysNothingWasFound()
    {
        var match = Match("Email");

        Assert.Equal(MatchReasons.NothingFound, match.Reason);
    }

    [Fact]
    public void Match_ConfidentValue_HasNoReason()
    {
        var match = Match("Email", Candidate("antoine@example.com", CandidateKind.Email, "Email"));

        Assert.Empty(match.Reason);
    }

    [Fact]
    public void Match_TwoValuesOfExpectedKind_PrefersTheOneWithMatchingLabel()
    {
        var match = Match(
            "Email",
            Candidate("contact@example.com", CandidateKind.Email, "Expéditeur"),
            Candidate("antoine@example.com", CandidateKind.Email, "Courriel"));

        AssertFills(match, "antoine@example.com");
    }

    [Fact]
    public void Match_SameLabelInTwoWindows_IsNotConfident()
    {
        var match = Match(
            "Email",
            Candidate("antoine@example.com", CandidateKind.Email, "Email", rank: 0),
            Candidate("contact@example.com", CandidateKind.Email, "Email", rank: 2));

        Assert.False(match.IsConfident);
    }

    [Fact]
    public void Match_SameValueInSeveralWindows_CountsAsOneChoice()
    {
        var match = Match(
            "Référence",
            Candidate("CMD-48213", CandidateKind.Reference, string.Empty, rank: 0),
            Candidate("CMD-48213", CandidateKind.Reference, "Commande n°", rank: 1));

        AssertFills(match, "CMD-48213");
    }

    [Fact]
    public void Match_NeverFillsValueOfAnotherRecognizedKind()
    {
        var match = Match("Email", Candidate("06 12 34 56 78", CandidateKind.Phone, "Email"));

        Assert.Null(match.Candidate);
        Assert.False(match.IsConfident);
    }

    [Fact]
    public void Match_TextValueWithMatchingLabel_IsConfident()
    {
        var match = Match(
            "Nom",
            Candidate("Antoine Barthélémy", CandidateKind.Text, "Nom"),
            Candidate("Bonjour Antoine,", CandidateKind.Text, string.Empty));

        AssertFills(match, "Antoine Barthélémy");
    }

    [Fact]
    public void Match_TextWithoutMatchingLabel_IsNeverChosen()
    {
        var match = Match(
            "Nom",
            Candidate("Bonjour Antoine,", CandidateKind.Text, string.Empty),
            Candidate("Reims", CandidateKind.Text, "Ville"));

        Assert.Null(match.Candidate);
    }

    [Fact]
    public void Match_PartlyMatchingLabel_IsNotConfident()
    {
        var match = Match("Nom de l'entreprise", Candidate("Antoine Barthélémy", CandidateKind.Text, "Nom"));

        Assert.False(match.IsConfident);
    }

    [Fact]
    public void Match_PostalCodeFoundInAddressLine()
    {
        var match = Match(
            "Code postal",
            Candidate("51500", CandidateKind.Number, string.Empty),
            Candidate("51500 Taissy", CandidateKind.Text, string.Empty));

        AssertFills(match, "51500");
    }

    [Fact]
    public void Match_PostalCodeFieldRejectsNumberThatIsNotAPostalCode()
    {
        var match = Match("Code postal", Candidate("00123", CandidateKind.Number, string.Empty));

        Assert.Null(match.Candidate);
    }

    [Fact]
    public void Match_PostalCodeFieldPrefersRealPostalCode()
    {
        var match = Match(
            "Code postal",
            Candidate("4521873", CandidateKind.Number, string.Empty),
            Candidate("51500", CandidateKind.Number, string.Empty));

        AssertFills(match, "51500");
    }

    [Fact]
    public void Match_DateOfBirth()
    {
        var match = Match(
            "Date de naissance",
            Candidate("12/03/2005", CandidateKind.Date, "Date de naissance"),
            Candidate("05/10/2026", CandidateKind.Date, "Envoyé le"));

        AssertFills(match, "12/03/2005");
    }

    [Fact]
    public void Match_FieldWithoutLabel_IsNeverConfident()
    {
        var match = Match(string.Empty, Candidate("antoine@example.com", CandidateKind.Email, "Email"));

        Assert.False(match.IsConfident);
    }

    [Fact]
    public void Match_UnknownFieldLabel_IsNeverConfidentWithoutMatchingLabel()
    {
        var match = Match("Commentaire", Candidate("CMD-48213", CandidateKind.Reference, "Commande n°"));

        Assert.False(match.IsConfident);
    }

    [Fact]
    public void Match_UsesTechnicalNameWhenLabelIsVague()
    {
        var field = TestFields.Text("field", "Contact") with { TechnicalName = "contact_email" };

        var match = FieldMatcher.Match(field, new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, string.Empty) });

        AssertFills(match, "antoine.b@orange.fr");
    }

    [Fact]
    public void Match_UsesTechnicalNameWhenFieldHasNoLabel()
    {
        var field = TestFields.Text("field", string.Empty) with { TechnicalName = "phoneNumber" };

        var match = FieldMatcher.Match(field, new[] { Candidate("06 47 21 85 39", CandidateKind.Phone, string.Empty) });

        AssertFills(match, "06 47 21 85 39");
    }

    [Fact]
    public void Match_UsesDateFormatShownInHint()
    {
        var field = TestFields.Text("field", "Le") with { Hint = "JJ/MM/AAAA" };

        var match = FieldMatcher.Match(field, new[] { Candidate("12/03/2005", CandidateKind.Date, string.Empty) });

        AssertFills(match, "12/03/2005");
    }

    [Fact]
    public void Match_UsesKindOfExampleShownInHint()
    {
        var field = TestFields.Text("field", "Identifiant") with { Hint = "ex : jean.dupont@exemple.fr" };

        var match = FieldMatcher.Match(field, new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, string.Empty) });

        AssertFills(match, "antoine.b@orange.fr");
    }

    [Fact]
    public void Match_TechnicalNameDoesNotOverrideTheLabel()
    {
        var field = TestFields.Text("field", "Téléphone") with { TechnicalName = "email" };

        var match = FieldMatcher.Match(field, new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") });

        Assert.Null(match.Candidate);
    }

    [Fact]
    public void Match_NeverChoosesTakenValue()
    {
        var match = FieldMatcher.Match(
            TestFields.Text("field", "Email"),
            new[] { Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email") },
            new HashSet<string> { "antoine.b@orange.fr" });

        Assert.Null(match.Candidate);
        Assert.False(match.IsConfident);
    }

    [Fact]
    public void Match_TakenValueStillPreventsTheOtherFromBeingUnique()
    {
        var match = FieldMatcher.Match(
            TestFields.Text("field", "Email"),
            new[]
            {
                Candidate("antoine.b@orange.fr", CandidateKind.Email, string.Empty),
                Candidate("contact@reims-auto.fr", CandidateKind.Email, string.Empty),
            },
            new HashSet<string> { "antoine.b@orange.fr" });

        Assert.Equal("contact@reims-auto.fr", match.Candidate?.Value);
        Assert.False(match.IsConfident);
    }

    [Fact]
    public void Match_TakenValueDoesNotHideAnotherValueThatFits()
    {
        var match = FieldMatcher.Match(
            TestFields.Text("field", "Email du parrain"),
            new[]
            {
                Candidate("antoine.b@orange.fr", CandidateKind.Email, "Email"),
                Candidate("lea.martin@free.fr", CandidateKind.Email, "Email du parrain"),
            },
            new HashSet<string> { "antoine.b@orange.fr" });

        AssertFills(match, "lea.martin@free.fr");
    }

    [Fact]
    public void Match_ProposesValuesOfExpectedKindEvenWhenNotSure()
    {
        var match = Match(
            "Email",
            Candidate("antoine.b@orange.fr", CandidateKind.Email, string.Empty),
            Candidate("contact@reims-auto.fr", CandidateKind.Email, string.Empty),
            Candidate("06 47 21 85 39", CandidateKind.Phone, string.Empty));

        Assert.False(match.IsConfident);
        Assert.Equal(new[] { "antoine.b@orange.fr", "contact@reims-auto.fr" }, match.Suggestions.Select(candidate => candidate.Value));
    }

    [Fact]
    public void Match_ProposesTheBestValuesFirstAndAtMostSome()
    {
        var match = Match(
            "Email",
            Candidate("a.martin@free.fr", CandidateKind.Email, string.Empty),
            Candidate("b.durand@free.fr", CandidateKind.Email, string.Empty),
            Candidate("antoine.b@orange.fr", CandidateKind.Email, "Courriel"),
            Candidate("c.petit@free.fr", CandidateKind.Email, string.Empty),
            Candidate("d.roux@free.fr", CandidateKind.Email, string.Empty));

        Assert.Equal(FieldMatcher.MaxSuggestions, match.Suggestions.Count);
        Assert.Equal("antoine.b@orange.fr", match.Suggestions[0].Value);
    }

    [Fact]
    public void Match_DoesNotProposeValuesThatOnlyHappenToBeOnScreen()
    {
        var match = Match(
            "Commentaire",
            Candidate("antoine.b@orange.fr", CandidateKind.Email, string.Empty),
            Candidate("Bonjour Antoine,", CandidateKind.Text, string.Empty));

        Assert.Empty(match.Suggestions);
    }

    [Fact]
    public void Match_NeverProposesTakenValues()
    {
        var match = FieldMatcher.Match(
            TestFields.Text("field", "Email"),
            new[]
            {
                Candidate("antoine.b@orange.fr", CandidateKind.Email, string.Empty),
                Candidate("contact@reims-auto.fr", CandidateKind.Email, string.Empty),
            },
            new HashSet<string> { "antoine.b@orange.fr" });

        Assert.Equal(new[] { "contact@reims-auto.fr" }, match.Suggestions.Select(candidate => candidate.Value));
    }

    [Fact]
    public void Match_WithoutCandidates_ReturnsNoCandidate()
    {
        var match = Match("Email");

        Assert.Null(match.Candidate);
        Assert.False(match.IsConfident);
    }

    private static FieldMatch Match(string fieldLabel, params Candidate[] candidates)
    {
        return FieldMatcher.Match(TestFields.Text("field", fieldLabel), candidates);
    }

    private static Candidate Candidate(string value, CandidateKind kind, string label, int rank = 1)
    {
        return new Candidate { Value = value, Kind = kind, Label = label, SourceName = "Gmail", SourceRank = rank };
    }

    private static void AssertFills(FieldMatch match, string expectedValue)
    {
        Assert.True(match.IsConfident, $"Not confident, score {match.Score}");
        Assert.Equal(expectedValue, match.Candidate?.Value);
    }
}
