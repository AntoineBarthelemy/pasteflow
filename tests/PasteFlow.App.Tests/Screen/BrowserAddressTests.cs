using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Automation;
using PasteFlow.App.Screen;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Tests.Screen;

/// <summary>
/// The address bar of a real Chrome window, on a sign-in page with a long address, as seen on 12/11/2026.
/// </summary>
public sealed class BrowserAddressTests
{
    private const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(30);

    [SkippableFact]
    public void TryRead_InARealChromeWindow_GivesTheSiteOfTheSignInPage()
    {
        Skip.IfNot(File.Exists(ChromePath), "Chrome n'est pas installé sur cette machine.");

        // A fake sign-in page served on this computer, under the name of a site
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = Task.Run(() => Serve(listener));
        var profile = Path.Combine(Path.GetTempPath(), "pasteflow-address-" + Guid.NewGuid().ToString("N"));
        var url = $"http://cas.univ-ville.fr:{port}/cas/login?service=https%3a%2f%2froundcube.univ-ville.fr%2fetud%2f%3f_task%3dmail";
        var start = new ProcessStartInfo(ChromePath)
        {
            ArgumentList =
            {
                $"--user-data-dir={profile}",
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-features=HttpsUpgrades,CalculateNativeWinOcclusion,Translate",
                "--disable-backgrounding-occluded-windows",
                "--host-resolver-rules=MAP cas.univ-ville.fr 127.0.0.1",
                "--new-window",
                url,
            },
            UseShellExecute = false,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Chrome n'a pas démarré.");
        try
        {
            var address = string.Empty;
            var deadline = DateTime.UtcNow + OpenTimeout;
            while (address.Length == 0 && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
                var window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
                address = window is null ? string.Empty : BrowserAddress.TryRead(window);
            }

            Assert.Equal("cas.univ-ville.fr", PageAddress.HostOf(address));
        }
        finally
        {
            try
            {
                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(5000);
            }
            catch (InvalidOperationException)
            {
                // Already gone
            }

            listener.Stop();
            try
            {
                Directory.Delete(profile, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A file of the profile is still held: it stays in the temporary folder
            }
        }
    }

    private static void Serve(TcpListener listener)
    {
        const string Body = "<!doctype html><title>CAS Login</title><form><label>Username:* <input name=u></label><label>Password:* <input type=password name=p></label></form>";
        try
        {
            while (true)
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                var buffer = new byte[8192];
                _ = stream.Read(buffer, 0, buffer.Length);
                stream.Write(Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(Body)}\r\nConnection: close\r\n\r\n{Body}"));
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or IOException or InvalidOperationException)
        {
            // The test is over: the listener was stopped
        }
    }
}
