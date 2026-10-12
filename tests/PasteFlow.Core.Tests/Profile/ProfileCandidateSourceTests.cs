using PasteFlow.Core.Candidates;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Profile;

public sealed class ProfileCandidateSourceTests : IDisposable
{
    private readonly FakeProfileStore _store = new();
    private readonly ProfileBook _book;

    public ProfileCandidateSourceTests()
    {
        _book = new ProfileBook(_store);
    }

    [Fact]
    public async Task Candidates_CarryTheirKindLabelAndUses()
    {
        _store.Preload(
            new ProfileValue { Topic = ProfileTopic.Email, Value = "antoine.b@orange.fr", UseCount = 4, Sites = "github.com=4" },
            new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189" });

        var candidates = await new ProfileCandidateSource(_book).GetCandidatesAsync(CancellationToken.None);

        var email = Assert.Single(candidates, candidate => candidate.Kind == CandidateKind.Email);
        Assert.Equal("E-mail", email.Label);
        Assert.True(email.IsFromProfile);
        Assert.False(email.IsSensitive);
        Assert.Equal(4, email.ProfileUseCount);
        Assert.Equal("github.com=4", email.ProfileSites);
        Assert.Equal(ProfileCandidateSource.SourceName, email.SourceName);
        Assert.True(Assert.Single(candidates, candidate => candidate.Kind == CandidateKind.Iban).IsSensitive);
    }

    [Fact]
    public async Task OneFirstNameAndOneLastName_MakeAWholeName()
    {
        _store.Preload(
            new ProfileValue { Topic = ProfileTopic.FirstName, Value = "Antoine" },
            new ProfileValue { Topic = ProfileTopic.LastName, Value = "Barthélémy" });

        var candidates = await new ProfileCandidateSource(_book).GetCandidatesAsync(CancellationToken.None);

        Assert.Equal("Antoine Barthélémy", Assert.Single(candidates, candidate => candidate.Kind == CandidateKind.FullName).Value);
    }

    [Fact]
    public async Task TwoLastNames_MakeNoWholeName()
    {
        _store.Preload(
            new ProfileValue { Topic = ProfileTopic.FirstName, Value = "Antoine" },
            new ProfileValue { Topic = ProfileTopic.LastName, Value = "Barthélémy" },
            new ProfileValue { Topic = ProfileTopic.LastName, Value = "Martin" });

        var candidates = await new ProfileCandidateSource(_book).GetCandidatesAsync(CancellationToken.None);

        Assert.DoesNotContain(candidates, candidate => candidate.Kind == CandidateKind.FullName);
    }

    [Fact]
    public void Other_IsOfferedUnderItsOwnName()
    {
        var (kind, label) = ProfileCandidateSource.DescriptionOf(new ProfileValue { Topic = ProfileTopic.Other, Value = "12345", Label = "N° étudiant" });

        Assert.Equal(CandidateKind.Text, kind);
        Assert.Equal("N° étudiant", label);
    }

    [Fact]
    public void EveryTopic_HasALabel()
    {
        Assert.All(ProfileTopics.All, topic => Assert.False(string.IsNullOrWhiteSpace(ProfileCandidateSource.DescriptionOf(new ProfileValue { Topic = topic, Value = "x" }).Label)));
    }

    public void Dispose()
    {
        _book.Dispose();
    }
}
