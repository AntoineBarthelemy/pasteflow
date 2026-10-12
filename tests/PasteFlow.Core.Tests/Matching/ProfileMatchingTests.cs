using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Matching;

public sealed class ProfileMatchingTests
{
    private const string MyEmail = "antoine.b@orange.fr";
    private const string SchoolEmail = "antoine.barthelemy@etudiant.univ-reims.fr";
    private const string RecruiterEmail = "recrutement@monstock.net";

    private static readonly PageContext SignUpPage = new() { Title = "Créer un compte - Welcome to the Jungle", Text = "Créer un compte\nE-mail\nMot de passe" };
    private static readonly PageContext NewCustomerPage = new() { Title = "Nouveau client - CRM", Text = "Nouveau client\nNom\nE-mail" };

    [Fact]
    public void FieldAboutTheUser_TakesTheProfileOverTheScreen()
    {
        var match = MatchOne(TestFields.Text("email", "Votre e-mail"), PageContext.Empty, Screen(RecruiterEmail, "E-mail"), Profile(MyEmail));

        AssertFills(match, MyEmail);
    }

    [Fact]
    public void FieldAboutSomeoneElse_NeverTakesTheProfile()
    {
        var match = MatchOne(TestFields.Text("email", "E-mail du contact"), PageContext.Empty, Screen(RecruiterEmail, "E-mail"), Profile(MyEmail));

        AssertFills(match, RecruiterEmail);
        Assert.DoesNotContain(match.Suggestions, candidate => candidate.IsFromProfile);
    }

    [Fact]
    public void FieldAboutSomeoneElse_StaysEmptyRatherThanTakingTheProfile()
    {
        var match = MatchOne(TestFields.Text("email", "E-mail du destinataire"), PageContext.Empty, Profile(MyEmail));

        Assert.False(match.IsConfident);
        Assert.Empty(match.Suggestions);
    }

    [Fact]
    public void NeutralField_WithTwoDifferentValues_IsLeftToTheUserWithBoth()
    {
        var match = MatchOne(TestFields.Text("email", "E-mail"), PageContext.Empty, Screen(RecruiterEmail, "E-mail"), Profile(MyEmail));

        Assert.False(match.IsConfident);
        Assert.Contains(match.Suggestions, candidate => candidate.Value == MyEmail);
        Assert.Contains(match.Suggestions, candidate => candidate.Value == RecruiterEmail);
    }

    [Fact]
    public void NeutralField_WithoutValueOnScreen_TakesTheProfile()
    {
        var match = MatchOne(TestFields.Text("email", "E-mail"), PageContext.Empty, Profile(MyEmail));

        AssertFills(match, MyEmail);
    }

    [Fact]
    public void NeutralField_WithTheSameValueOnScreen_IsFilled()
    {
        var match = MatchOne(TestFields.Text("email", "E-mail"), PageContext.Empty, Screen(MyEmail, "E-mail"), Profile(MyEmail));

        AssertFills(match, MyEmail);
    }

    [Fact]
    public void NeutralField_OnASignUpPage_TakesTheProfile()
    {
        var match = MatchOne(TestFields.Text("email", "E-mail"), SignUpPage, Screen(RecruiterEmail, "E-mail"), Profile(MyEmail));

        AssertFills(match, MyEmail);
    }

    [Fact]
    public void NeutralField_OnANewCustomerPage_TakesTheScreen()
    {
        var match = MatchOne(TestFields.Text("email", "E-mail"), NewCustomerPage, Screen(RecruiterEmail, "E-mail"), Profile(MyEmail));

        AssertFills(match, RecruiterEmail);
    }

    [Fact]
    public void CopyFromAFormBehind_TakesTheScreen()
    {
        var copied = Screen(RecruiterEmail, "E-mail") with { IsTypedInField = true };

        var match = MatchOne(TestFields.Text("email", "E-mail"), SignUpPage, copied, Profile(MyEmail));

        AssertFills(match, RecruiterEmail);
    }

    [Fact]
    public void TwoValuesOfTheProfile_WithoutPreference_AreOnlyProposed()
    {
        var match = MatchOne(TestFields.Text("email", "Votre e-mail"), PageContext.Empty, Profile(MyEmail), Profile(SchoolEmail));

        Assert.False(match.IsConfident);
        Assert.Contains(match.Suggestions, candidate => candidate.Value == MyEmail);
        Assert.Contains(match.Suggestions, candidate => candidate.Value == SchoolEmail);
    }

    [Fact]
    public void TwoValuesOfTheProfile_TheOneWrittenOnThisSiteWins()
    {
        var page = new PageContext { Host = "moodle.univ-reims.fr" };

        var match = MatchOne(
            TestFields.Text("email", "Votre e-mail"),
            page,
            Profile(MyEmail, uses: 9),
            Profile(SchoolEmail, uses: 2, sites: "univ-reims.fr=2"));

        AssertFills(match, SchoolEmail);
        Assert.Contains(match.Suggestions, candidate => candidate.Value == MyEmail);
    }

    [Fact]
    public void TwoValuesOfTheProfile_ElsewhereTheMostUsedWins()
    {
        var match = MatchOne(
            TestFields.Text("email", "Votre e-mail"),
            new PageContext { Host = "github.com" },
            Profile(MyEmail, uses: 9),
            Profile(SchoolEmail, uses: 2, sites: "univ-reims.fr=2"));

        AssertFills(match, MyEmail);
    }

    [Fact]
    public void LinkedInField_TakesOnlyTheLinkedInAddress()
    {
        var linkedIn = Profile("linkedin.com/in/antoine-barthelemy", CandidateKind.Url, "LinkedIn");
        var portfolio = Profile("antoinebarthelemy.dev", CandidateKind.Url, "Site web");

        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("linkedin", "Profil LinkedIn"), TestFields.Text("site", "Site web") },
            new[] { linkedIn, portfolio },
            ReferenceData.Empty,
            MatchingOptions.Balanced,
            Array.Empty<FormField>(),
            SignUpPage);

        AssertFills(matches[0], linkedIn.Value);
        AssertFills(matches[1], portfolio.Value);
    }

    [Fact]
    public void NameAlone_TakesTheWholeNameOfTheProfile()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("name", "Nom"), TestFields.Text("email", "E-mail") },
            new[]
            {
                Profile("Antoine", CandidateKind.FirstName, "Prénom"),
                Profile("Barthélémy", CandidateKind.LastName, "Nom"),
                Profile("Antoine Barthélémy", CandidateKind.FullName, "Nom complet"),
                Profile(MyEmail),
            },
            ReferenceData.Empty,
            MatchingOptions.Balanced,
            Array.Empty<FormField>(),
            SignUpPage);

        AssertFills(matches[0], "Antoine Barthélémy");
        AssertFills(matches[1], MyEmail);
    }

    [Fact]
    public void FirstAndLastName_TakeTheirOwnValues()
    {
        var fields = new[] { TestFields.Text("first", "Prénom"), TestFields.Text("last", "Nom") };

        var matches = FormMatcher.MatchAll(
            fields,
            new[]
            {
                Profile("Antoine", CandidateKind.FirstName, "Prénom"),
                Profile("Barthélémy", CandidateKind.LastName, "Nom"),
                Profile("Antoine Barthélémy", CandidateKind.FullName, "Nom complet"),
            },
            ReferenceData.Empty,
            MatchingOptions.Balanced,
            fields,
            SignUpPage);

        AssertFills(matches[0], "Antoine");
        AssertFills(matches[1], "Barthélémy");
    }

    [Fact]
    public void SponsorField_NeverTakesTheUsersOwnEmail()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "E-mail"), TestFields.Text("sponsor", "E-mail du parrain") },
            new[] { Profile(MyEmail) },
            ReferenceData.Empty,
            MatchingOptions.Balanced,
            Array.Empty<FormField>(),
            SignUpPage);

        AssertFills(matches[0], MyEmail);
        Assert.False(matches[1].IsConfident);
    }

    [Fact]
    public void WithoutProfile_NothingChanges()
    {
        var match = MatchOne(TestFields.Text("email", "Votre e-mail"), SignUpPage, Screen(RecruiterEmail, "E-mail"));

        AssertFills(match, RecruiterEmail);
    }

    private static FieldMatch MatchOne(FormField field, PageContext page, params Candidate[] candidates)
    {
        return FormMatcher.MatchAll(new[] { field }, candidates, ReferenceData.Empty, MatchingOptions.Balanced, new[] { field }, page)[0];
    }

    private static Candidate Screen(string value, string label)
    {
        return new Candidate { Value = value, Kind = CandidateKind.Email, Label = label, SourceName = "Gmail", SourceRank = 1 };
    }

    private static Candidate Profile(string value, int uses = 0, string sites = "")
    {
        return Profile(value, CandidateKind.Email, "E-mail") with { ProfileUseCount = uses, ProfileSites = sites };
    }

    private static Candidate Profile(string value, CandidateKind kind, string label)
    {
        return new Candidate { Value = value, Kind = kind, Label = label, SourceName = "Ma fiche", SourceRank = 0, IsFromProfile = true };
    }

    private static void AssertFills(FieldMatch match, string value)
    {
        Assert.True(match.IsConfident, $"The field should be filled with {value}.");
        Assert.Equal(value, match.Candidate?.Value);
    }
}
