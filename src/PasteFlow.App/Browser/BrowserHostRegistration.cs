using System.IO;
using System.Security;
using System.Text.Json;
using Microsoft.Win32;

namespace PasteFlow.App.Browser;

/// <summary>
/// Tells Chrome and Edge where PasteFlow is, so that its extension can reach it (native messaging): a small file
/// describes the relay, and a key of the registry of the current user points at it. No administrator right is needed.
/// </summary>
internal static class BrowserHostRegistration
{
    /// <summary>
    /// The name the extension uses to reach PasteFlow.
    /// </summary>
    public const string HostName = "com.pasteflow.host";

    /// <summary>
    /// The identifier of the extension, set by the key of its manifest: the only extension allowed to reach PasteFlow.
    /// </summary>
    public const string ExtensionId = "kdmkgjlkgadkmjjcjfcpgpdiiadnflkn";

    private static readonly string[] BrowserKeys =
    {
        @"Software\Google\Chrome\NativeMessagingHosts",
        @"Software\Microsoft\Edge\NativeMessagingHosts",
    };

    /// <summary>
    /// Gets the file that describes the relay: %LOCALAPPDATA%\PasteFlow\browser-host.json.
    /// </summary>
    public static string ManifestPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "browser-host.json");

    /// <summary>
    /// Registers PasteFlow with Chrome and Edge, again at each launch so that the path stays right after an update.
    /// </summary>
    /// <param name="executablePath">The path of PasteFlow.</param>
    /// <returns><see langword="true"/> if it is registered; otherwise <see langword="false"/>.</returns>
    public static bool TryRegister(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        try
        {
            var manifestPath = ManifestPath;
            _ = Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, ManifestJson(executablePath));
            foreach (var browserKey in BrowserKeys)
            {
                using var key = Registry.CurrentUser.CreateSubKey($@"{browserKey}\{HostName}");
                key.SetValue(string.Empty, manifestPath);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the description of the relay that browsers read.
    /// </summary>
    /// <param name="executablePath">The path of PasteFlow.</param>
    /// <returns>The JSON text of the description.</returns>
    internal static string ManifestJson(string executablePath)
    {
        return JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["name"] = HostName,
                ["description"] = "PasteFlow",
                ["path"] = executablePath,
                ["type"] = "stdio",
                ["allowed_origins"] = new[] { $"chrome-extension://{ExtensionId}/" },
            },
            new JsonSerializerOptions { WriteIndented = true });
    }
}
