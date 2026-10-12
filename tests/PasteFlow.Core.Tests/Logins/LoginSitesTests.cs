using PasteFlow.Core.Logins;

namespace PasteFlow.Core.Tests.Logins;

public sealed class LoginSitesTests
{
    [Theory]
    [InlineData("https://github.com/login", "github.com")]
    [InlineData("github.com", "gist.github.com")]
    [InlineData("https://accounts.google.com", "mail.google.com")]
    [InlineData("https://www.univ-reims.fr", "ent.univ-reims.fr")]
    public void AccountOfTheSameSite_Fits(string uri, string host)
    {
        Assert.True(LoginSites.IsFor(Login(uri), host));
    }

    [Theory]
    [InlineData("https://github.com", "github.com.fake.ru")]
    [InlineData("https://github.com", "githuh.com")]
    [InlineData("https://paypal.com", "paypal-secure-login.com")]
    [InlineData("https://antoine.github.io", "pirate.github.io")]
    [InlineData("androidapp://com.github.android", "github.com")]
    [InlineData("https://github.com", "")]
    public void AccountOfAnotherSite_NeverFits(string uri, string host)
    {
        Assert.False(LoginSites.IsFor(Login(uri), host));
    }

    private static VaultLogin Login(string uri)
    {
        return new VaultLogin { Id = "1", Name = "Compte", Uris = new[] { uri } };
    }
}
