using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Profile;

public sealed class SiteUsesTests
{
    [Fact]
    public void Add_CountsAndPutsTheSiteFirst()
    {
        var sites = SiteUses.Add(string.Empty, "github.com");
        sites = SiteUses.Add(sites, "univ-reims.fr");
        sites = SiteUses.Add(sites, "github.com");

        Assert.Equal("github.com=2 univ-reims.fr=1", sites);
        Assert.Equal(2, SiteUses.CountFor(sites, "github.com"));
        Assert.Equal(0, SiteUses.CountFor(sites, "linkedin.com"));
        Assert.Equal(0, SiteUses.CountFor(sites, string.Empty));
    }

    [Fact]
    public void Add_KeepsTheMostRecentSitesOnly()
    {
        var sites = string.Empty;
        for (var index = 0; index <= SiteUses.MaxSites; index++)
        {
            sites = SiteUses.Add(sites, $"site{index}.fr");
        }

        Assert.Equal(SiteUses.MaxSites, SiteUses.Read(sites).Count);
        Assert.Equal(0, SiteUses.CountFor(sites, "site0.fr"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("deux mots")]
    [InlineData("a=b")]
    public void Add_IgnoresWhatIsNotASite(string site)
    {
        Assert.Equal("github.com=1", SiteUses.Add("github.com=1", site));
    }

    [Fact]
    public void Read_SkipsDamagedParts()
    {
        Assert.Single(SiteUses.Read("github.com=2 abimé =3 x=0 y=-1"));
    }
}
