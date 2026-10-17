using PasteFlow.App.Tray;

namespace PasteFlow.App.Tests.Tray;

public sealed class TrayIconTests
{
    [Fact]
    public void BitwardenMenuText_TellsTheStateOfTheVault()
    {
        Assert.Equal("Déverrouiller Bitwarden…", TrayIcon.BitwardenMenuText(false));
        Assert.StartsWith("Bitwarden déverrouillé", TrayIcon.BitwardenMenuText(true), StringComparison.Ordinal);
    }
}
