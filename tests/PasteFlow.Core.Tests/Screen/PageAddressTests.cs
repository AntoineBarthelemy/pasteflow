using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Tests.Screen;

public sealed class PageAddressTests
{
    [Theory]
    [InlineData("github.com/login", "github.com")]
    [InlineData("https://www.univ-reims.fr/", "univ-reims.fr")]
    [InlineData("http://moodle.univ-reims.fr/course/view.php?id=3", "moodle.univ-reims.fr")]
    [InlineData("  Accounts.Google.com  ", "accounts.google.com")]
    public void HostOf_ReadsTheHost(string address, string expected)
    {
        Assert.Equal(expected, PageAddress.HostOf(address));
    }

    [Theory]
    [InlineData("")]
    [InlineData("météo reims")]
    [InlineData("localhost:3000")]
    [InlineData("file:///C:/Users/User/doc.html")]
    [InlineData("ftp://example.com")]
    [InlineData("192.168.1.1")]
    public void HostOf_OfSomethingElse_IsEmpty(string address)
    {
        Assert.Equal(string.Empty, PageAddress.HostOf(address));
    }

    [Theory]
    [InlineData("moodle.univ-reims.fr", "univ-reims.fr")]
    [InlineData("univ-reims.fr", "univ-reims.fr")]
    [InlineData("www.service-public.gouv.fr", "service-public.gouv.fr")]
    [InlineData("bbc.co.uk", "bbc.co.uk")]
    [InlineData("", "")]
    public void SiteOf_KeepsTheNameOfTheSite(string host, string expected)
    {
        Assert.Equal(expected, PageAddress.SiteOf(host));
    }
}
