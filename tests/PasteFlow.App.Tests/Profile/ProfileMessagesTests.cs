using PasteFlow.App.Profile;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Security;

namespace PasteFlow.App.Tests.Profile;

public sealed class ProfileMessagesTests
{
    [Fact]
    public void EveryResult_HasItsOwnSentence()
    {
        var sentences = Enum.GetValues<ProfileEditResult>().Select(ProfileMessages.For).ToList();

        Assert.All(sentences, sentence => Assert.False(string.IsNullOrWhiteSpace(sentence)));
        Assert.Equal(sentences.Count, sentences.Distinct().Count());
        Assert.DoesNotContain(sentences, sentence => sentence.Contains('—', StringComparison.Ordinal));
    }

    [Fact]
    public void EveryPresenceResult_HasItsOwnSentence()
    {
        var sentences = Enum.GetValues<PresenceResult>().Select(ProfileMessages.For).ToList();

        Assert.Equal(sentences.Count, sentences.Distinct().Count());
        Assert.Contains("PIN", ProfileMessages.For(PresenceResult.Unavailable), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, true, "Rien de nouveau : tout était déjà dans ta fiche.")]
    [InlineData(1, true, "1 info ajoutée à ta fiche.")]
    [InlineData(3, true, "3 infos ajoutées à ta fiche.")]
    [InlineData(3, false, "3 infos ajoutées à ta fiche. Attention, elles n'ont pas pu être enregistrées.")]
    public void ImportSummary_SaysWhatWasAdded(int added, bool isSaved, string expected)
    {
        Assert.Equal(expected, ProfileMessages.For(new ProfileImportSummary { Added = added, IsSaved = isSaved }));
    }
}
