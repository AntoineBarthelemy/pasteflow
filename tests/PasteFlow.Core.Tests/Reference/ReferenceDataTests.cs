using PasteFlow.Core.Reference;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Reference;

public sealed class ReferenceDataTests
{
    private readonly ReferenceData _data = TestReferenceData.Create();

    [Theory]
    [InlineData("Antoine", true)]
    [InlineData("ANTOINE", true)]
    [InlineData("jean-pierre", true)]
    [InlineData("Barthélémy", false)]
    [InlineData("", false)]
    public void IsFirstName_IgnoresCaseAndAccents(string name, bool expected)
    {
        Assert.Equal(expected, _data.IsFirstName(name));
    }

    [Theory]
    [InlineData("51500", "Taissy", true)]
    [InlineData("51100", "REIMS CEDEX 2", true)]
    [InlineData("85160", "Saint-Jean-de-Monts", true)]
    [InlineData("51000", "Châlons-en-Champagne", true)]
    [InlineData("75010", "Paris", true)]
    [InlineData("75010", "Paris 10", true)]
    [InlineData("51500", "Reims", false)]
    [InlineData("99999", "Reims", false)]
    public void IsCommuneOf_ChecksTheCommuneGoesWithThePostalCode(string postalCode, string commune, bool expected)
    {
        Assert.Equal(expected, _data.IsCommuneOf(postalCode, commune));
    }

    [Theory]
    [InlineData("Reims", true)]
    [InlineData("reims cedex", true)]
    [InlineData("Bezannes", false)]
    public void IsCommune_FindsKnownCommunes(string name, bool expected)
    {
        Assert.Equal(expected, _data.IsCommune(name));
    }

    [Theory]
    [InlineData("51500", true)]
    [InlineData(" 51100 ", true)]
    [InlineData("99999", false)]
    public void IsPostalCode_FindsKnownPostalCodes(string postalCode, bool expected)
    {
        Assert.Equal(expected, _data.IsPostalCode(postalCode));
    }

    [Theory]
    [InlineData("France", true)]
    [InlineData("GERMANY", true)]
    [InlineData("Reims", false)]
    public void IsCountry_FindsKnownCountries(string name, bool expected)
    {
        Assert.Equal(expected, _data.IsCountry(name));
    }

    [Fact]
    public void Constructor_SkipsMalformedLines()
    {
        var data = new ReferenceData(new[] { " " }, new[] { "no separator", ";REIMS", "51100;" }, Array.Empty<string>());

        Assert.False(data.IsCommune("REIMS"));
        Assert.False(data.IsPostalCode("51100"));
        Assert.False(data.IsFirstName(" "));
    }

    [Fact]
    public void Empty_KnowsNothing()
    {
        Assert.False(ReferenceData.Empty.IsFirstName("Antoine"));
        Assert.False(ReferenceData.Empty.IsCommune("Reims"));
        Assert.False(ReferenceData.Empty.IsCountry("France"));
    }

    [Fact]
    public void Shared_HoldsTheOfficialLists()
    {
        var data = ReferenceData.Shared;

        Assert.True(data.IsFirstName("Antoine"));
        Assert.True(data.IsFirstName("Léa"));
        Assert.True(data.IsFirstName("Jean-Pierre"));
        Assert.True(data.IsCommuneOf("51500", "Taissy"));
        Assert.True(data.IsCommuneOf("51100", "Reims"));
        Assert.True(data.IsCommuneOf("51000", "Châlons-en-Champagne"));
        Assert.True(data.IsCommuneOf("75010", "Paris"));
        Assert.True(data.IsCommuneOf("69003", "Lyon"));
        Assert.True(data.IsCommuneOf("13008", "Marseille"));
        Assert.True(data.IsCountry("Allemagne"));
        Assert.True(data.IsCountry("Germany"));
        Assert.False(data.IsFirstName("Dupont"));
        Assert.False(data.IsFirstName("Lefèvre"));
    }
}
