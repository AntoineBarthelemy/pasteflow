using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Profile;

public sealed class SensitiveFormatsTests
{
    [Theory]
    [InlineData("FR76 3000 6000 0112 3456 7890 189")]
    [InlineData("FR7630006000011234567890189")]
    [InlineData("fr76 3000 6000 0112 3456 7890 189")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    public void IsIban_WithACorrectKey_IsTrue(string value)
    {
        Assert.True(SensitiveFormats.IsIban(value));
        Assert.True(SensitiveFormats.IsSensitiveValue(value));
    }

    [Theory]
    [InlineData("FR76 3000 6000 0112 3456 7890 188")]
    [InlineData("3000 6000 0112 3456 7890 189")]
    [InlineData("FR76")]
    [InlineData("")]
    public void IsIban_WithoutTheShapeOrTheKey_IsFalse(string value)
    {
        Assert.False(SensitiveFormats.IsIban(value));
    }

    [Theory]
    [InlineData("1 90 03 51 454 123 62")]
    [InlineData("285117511600702")]
    [InlineData("2.85.11.75.116.007.02")]
    [InlineData("1 85 02 2A 123 456 12")]
    public void IsFrenchSocialSecurityNumber_WithACorrectKey_IsTrue(string value)
    {
        Assert.True(SensitiveFormats.IsFrenchSocialSecurityNumber(value));
        Assert.True(SensitiveFormats.IsSensitiveValue(value));
    }

    [Theory]
    [InlineData("1 90 03 51 454 123 63")]
    [InlineData("5 90 03 51 454 123 62")]
    [InlineData("1 90 03 51 454 123")]
    [InlineData("06 47 21 85 39")]
    public void IsFrenchSocialSecurityNumber_WithAWrongKeyOrLength_IsFalse(string value)
    {
        Assert.False(SensitiveFormats.IsFrenchSocialSecurityNumber(value));
    }

    [Theory]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("3782 822463 10005")]
    public void IsPaymentCardNumber_WithALuhnKey_IsTrue(string value)
    {
        Assert.True(SensitiveFormats.IsPaymentCardNumber(value));
        Assert.True(SensitiveFormats.IsSensitiveValue(value));
    }

    [Theory]
    [InlineData("4111 1111 1111 1112")]
    [InlineData("732 829 320 00074")]
    [InlineData("1 90 03 51 454 123 62")]
    [InlineData("06 47 21 85 39")]
    public void IsPaymentCardNumber_ForOtherNumbers_IsFalse(string value)
    {
        Assert.False(SensitiveFormats.IsPaymentCardNumber(value));
    }

    [Theory]
    [InlineData("antoine.b@orange.fr")]
    [InlineData("06 47 21 85 39")]
    [InlineData("51500")]
    [InlineData("Antoine Barthélémy")]
    public void IsSensitiveValue_ForOrdinaryValues_IsFalse(string value)
    {
        Assert.False(SensitiveFormats.IsSensitiveValue(value));
    }
}
