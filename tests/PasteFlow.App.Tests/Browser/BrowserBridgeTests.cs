using System.IO;
using System.IO.Pipes;
using PasteFlow.App.Browser;
using PasteFlow.Core.Browser;

namespace PasteFlow.App.Tests.Browser;

public sealed class BrowserBridgeTests
{
    private const string Tabs = """{"title":"Gmail","url":"https://mail.google.com/","text":"lea.martin@atelier-nova.fr","links":[],"lastAccessed":1}""";

    [Fact]
    public async Task ReadTabsAsync_WithoutBrowser_GivesNothingAtOnce()
    {
        using var bridge = new BrowserBridge(UniquePipeName());

        Assert.Empty(await bridge.ReadTabsAsync(TimeSpan.FromSeconds(1), CancellationToken.None));
    }

    [Fact]
    public async Task ReadTabsAsync_GoesThroughTheRelayToTheExtensionAndBack()
    {
        var pipeName = UniquePipeName();
        using var bridge = new BrowserBridge(pipeName);

        // The browser side of the relay: what the extension receives, and what it answers
        using var toExtension = new AnonymousPipeServerStream(PipeDirection.In);
        using var relayOut = new AnonymousPipeClientStream(PipeDirection.Out, toExtension.ClientSafePipeHandle);
        using var fromExtension = new AnonymousPipeServerStream(PipeDirection.Out);
        using var relayIn = new AnonymousPipeClientStream(PipeDirection.In, fromExtension.ClientSafePipeHandle);
        using var stop = new CancellationTokenSource();
        var relay = Task.Run(() => NativeHost.RelayAsync(relayIn, relayOut, pipeName, stop.Token));
        var extension = Task.Run(async () =>
        {
            var request = await NativeMessages.ReadAsync(toExtension, CancellationToken.None);
            var id = System.Text.Json.JsonDocument.Parse(request!).RootElement.GetProperty("id").GetInt32();
            await NativeMessages.WriteAsync(fromExtension, $$"""{"type":"tabs","id":{{id}},"tabs":[{{Tabs}}]}""", CancellationToken.None);
        });

        await WaitUntilAsync(() => bridge.ConnectedCount == 1);
        var tabs = await bridge.ReadTabsAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        await extension;

        Assert.Equal("Gmail", Assert.Single(tabs).Title);

        // The browser closes: the relay ends
        fromExtension.Dispose();
        await stop.CancelAsync();
        await relay.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RelayAsync_WithoutPasteFlow_EndsAtOnce()
    {
        // Left running, the relay would keep the files of PasteFlow in use and block its updates
        using var fromExtension = new AnonymousPipeServerStream(PipeDirection.Out);
        using var relayIn = new AnonymousPipeClientStream(PipeDirection.In, fromExtension.ClientSafePipeHandle);
        using var relayOut = new MemoryStream();

        await NativeHost.RelayAsync(relayIn, relayOut, UniquePipeName(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void IsStartedByBrowser_RecognizesTheCallOfAnExtension()
    {
        Assert.True(NativeHost.IsStartedByBrowser(new[] { "chrome-extension://kdmkgjlkgadkmjjcjfcpgpdiiadnflkn/", "--parent-window=0" }));
        Assert.False(NativeHost.IsStartedByBrowser(Array.Empty<string>()));
    }

    [Fact]
    public void ManifestJson_OnlyAllowsTheExtensionOfPasteFlow()
    {
        var json = BrowserHostRegistration.ManifestJson(@"C:\PasteFlow\PasteFlow.App.exe");

        Assert.Contains("\"name\": \"com.pasteflow.host\"", json, StringComparison.Ordinal);
        Assert.Contains("chrome-extension://kdmkgjlkgadkmjjcjfcpgpdiiadnflkn/", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"stdio\"", json, StringComparison.Ordinal);
    }

    private static string UniquePipeName()
    {
        return $"PasteFlow.Tests.{Guid.NewGuid():N}";
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(50);
        }
    }
}
