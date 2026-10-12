using PasteFlow.App.Logins;
using PasteFlow.Core.Logins;

namespace PasteFlow.App.Tests.Logins;

public sealed class BitwardenCliTests
{
    [Theory]
    [InlineData("github.com")]
    [InlineData("https://github.com")]
    [InlineData("steam")]
    [InlineData("3f2b1c4e-90ab-4cde-8f01-23456789abcd")]
    [InlineData("Espace numérique")]
    public void SafeArguments_AreAccepted(string word)
    {
        Assert.True(BitwardenCli.IsSafeArgument(word));
    }

    [Theory]
    [InlineData("")]
    [InlineData("steam & del *")]
    [InlineData("a|b")]
    [InlineData("%PATH%")]
    [InlineData("\"quoted\"")]
    [InlineData("a^b")]
    [InlineData("a<b>")]
    public void DangerousArguments_AreRefused(string word)
    {
        Assert.False(BitwardenCli.IsSafeArgument(word));
    }

    [Fact]
    public async Task WithoutBitwarden_NothingIsFound()
    {
        var bitwarden = new BitwardenCli(executable: null);

        Assert.False(bitwarden.IsInstalled);
        Assert.False(bitwarden.IsUnlocked);
        Assert.Equal(string.Empty, await bitwarden.StatusAsync(CancellationToken.None));
        Assert.False(await bitwarden.UnlockAsync("mot-de-passe-maitre", CancellationToken.None));
        Assert.Empty(await bitwarden.FindLoginsForSiteAsync("github.com", CancellationToken.None));
        Assert.Null(await bitwarden.GetPasswordAsync("3f2b1c4e", CancellationToken.None));
    }

    [Fact]
    public void LoginLine_ShowsTheNameAndTheIdentifier()
    {
        Assert.Equal("GitHub (AntoineBarthelemy)", LoginPickerWindow.Line(new VaultLogin { Id = "1", Name = "GitHub", UserName = "AntoineBarthelemy" }));
        Assert.Equal("Steam", LoginPickerWindow.Line(new VaultLogin { Id = "2", Name = "Steam" }));
    }
}
