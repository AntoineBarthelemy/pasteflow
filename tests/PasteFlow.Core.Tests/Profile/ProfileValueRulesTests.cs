using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Profile;

public sealed class ProfileValueRulesTests
{
    [Theory]
    [InlineData(ProfileTopic.FirstName, "Antoine")]
    [InlineData(ProfileTopic.Email, "antoine.b@orange.fr")]
    [InlineData(ProfileTopic.Phone, "06 47 21 85 39")]
    [InlineData(ProfileTopic.Iban, "FR76 3000 6000 0112 3456 7890 189")]
    [InlineData(ProfileTopic.SocialSecurityNumber, "1 90 03 51 454 123 62")]
    [InlineData(ProfileTopic.Other, "732 829 320 00074")]
    public void Check_AcceptsValuesThatFitTheirTopic(ProfileTopic topic, string value)
    {
        Assert.Equal(ProfileEditResult.Done, ProfileValueRules.Check(topic, value));
    }

    [Theory]
    [InlineData(ProfileTopic.Email, "antoine.orange.fr")]
    [InlineData(ProfileTopic.Iban, "FR76 3000 6000 0112 3456 7890 188")]
    [InlineData(ProfileTopic.SocialSecurityNumber, "1 90 03 51 454 123 63")]
    public void Check_RefusesValuesWithTheWrongFormat(ProfileTopic topic, string value)
    {
        Assert.Equal(ProfileEditResult.InvalidFormat, ProfileValueRules.Check(topic, value));
    }

    [Theory]
    [InlineData(ProfileTopic.Other)]
    [InlineData(ProfileTopic.Phone)]
    public void Check_NeverKeepsAPaymentCard(ProfileTopic topic)
    {
        Assert.Equal(ProfileEditResult.PaymentCardRefused, ProfileValueRules.Check(topic, "4111 1111 1111 1111"));
    }

    [Fact]
    public void Check_RefusesEmptyAndTooLongValues()
    {
        Assert.Equal(ProfileEditResult.Empty, ProfileValueRules.Check(ProfileTopic.City, "   "));
        Assert.Equal(ProfileEditResult.TooLong, ProfileValueRules.Check(ProfileTopic.City, new string('a', ProfileValueRules.MaxValueLength + 1)));
    }

    [Theory]
    [InlineData(ProfileTopic.Iban, "FR76 3000 6000 0112 3456 7890 189", "FR7630006000011234567890189")]
    [InlineData(ProfileTopic.Phone, "06 47 21 85 39", "06.47.21.85.39")]
    [InlineData(ProfileTopic.Email, "antoine.b@orange.fr", "Antoine.B@Orange.fr")]
    [InlineData(ProfileTopic.Street, "12  rue des Vignes", "12 rue des vignes")]
    public void AreSame_IgnoresHowTheValueIsWritten(ProfileTopic topic, string first, string second)
    {
        Assert.True(ProfileValueRules.AreSame(topic, first, second));
    }

    [Fact]
    public void AreSame_TellsDifferentValuesApart()
    {
        Assert.False(ProfileValueRules.AreSame(ProfileTopic.Email, "antoine.b@orange.fr", "antoine.barthelemy@etudiant.univ-reims.fr"));
        Assert.False(ProfileValueRules.AreSame(ProfileTopic.Street, "12 rue des Vignes", "12 rue desVignes"));
    }
}
