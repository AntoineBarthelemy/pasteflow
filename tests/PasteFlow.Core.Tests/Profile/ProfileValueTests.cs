using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Profile;

public sealed class ProfileValueTests
{
    [Fact]
    public void OrdinaryValue_IsShownWhole()
    {
        var value = new ProfileValue { Topic = ProfileTopic.Email, Value = "antoine.b@orange.fr" };

        Assert.False(value.IsSensitive);
        Assert.Equal("antoine.b@orange.fr", value.ShownValue);
        Assert.Equal("E-mail", value.ShownLabel);
    }

    [Fact]
    public void SensitiveTopic_ShowsOnlyTheLastCharacters()
    {
        var value = new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189" };

        Assert.True(value.IsSensitive);
        Assert.Equal("•••• 0189", value.ShownValue);
    }

    [Fact]
    public void SensitiveFormat_IsSensitiveWhateverItsTopic()
    {
        var value = new ProfileValue { Topic = ProfileTopic.Other, Value = "1 90 03 51 454 123 62", Label = "Sécu" };

        Assert.True(value.IsSensitive);
        Assert.DoesNotContain("454", value.ShownValue, StringComparison.Ordinal);
        Assert.Equal("Sécu", value.ShownLabel);
    }

    [Fact]
    public void ShortSensitiveValue_IsHiddenEntirely()
    {
        var value = new ProfileValue { Topic = ProfileTopic.PassportNumber, Value = "18AB12345" };

        Assert.Equal("•••• 2345", value.ShownValue);
        Assert.Equal("••••", (value with { Value = "AB1234" }).ShownValue);
    }

    [Fact]
    public void EveryTopic_HasAFrenchName()
    {
        Assert.All(ProfileTopics.All, topic => Assert.False(string.IsNullOrWhiteSpace(ProfileTopics.DisplayName(topic))));
        Assert.Equal(ProfileTopics.All.Count, ProfileTopics.All.Select(ProfileTopics.DisplayName).Distinct().Count());
    }
}
