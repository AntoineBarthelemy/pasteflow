using PasteFlow.App.Tray;

namespace PasteFlow.App.Tests.Tray;

public sealed class TrayIconTests
{
    [Fact]
    public void Icon_IsEmbeddedInTheProgram()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream("PasteFlow.App.Assets.PasteFlow.ico");

        Assert.NotNull(stream);
        Assert.True(stream.Length > 1000);
    }
}
