using PasteFlow.App.Profile;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Tests.Profile;

public sealed class PopupLearningPromptsTests
{
    [Fact]
    public void AddedSentence_ShowsTheValue()
    {
        var sentence = PopupLearningPrompts.AddedSentence(new ProfileValue { Topic = ProfileTopic.Email, Value = "antoine.b@orange.fr" });

        Assert.Equal("Ajouté à ta fiche : E-mail, antoine.b@orange.fr", sentence);
    }

    [Fact]
    public void Sentences_NeverShowASensitiveValueWhole()
    {
        var iban = new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189" };

        Assert.DoesNotContain("3000", PopupLearningPrompts.AddedSentence(iban), StringComparison.Ordinal);
        Assert.DoesNotContain("3000", PopupLearningPrompts.SensitiveQuestion(iban), StringComparison.Ordinal);
        Assert.Contains("0189", PopupLearningPrompts.SensitiveQuestion(iban), StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveQuestion_SaysWhereTheValueWasSeen()
    {
        var iban = new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189" };

        Assert.StartsWith("Tu as rempli deux fois", PopupLearningPrompts.SensitiveQuestion(iban), StringComparison.Ordinal);
        Assert.StartsWith("Ton identité Bitwarden", PopupLearningPrompts.SensitiveQuestion(iban with { Origin = ProfileOrigin.PasswordManager }), StringComparison.Ordinal);
    }
}
