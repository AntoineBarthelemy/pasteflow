using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PasteFlow.Core.Settings;

namespace PasteFlow.App.Settings;

/// <summary>
/// Keeps the settings of the user in a readable JSON file: they hold nothing private.
/// </summary>
internal sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSettingsStore"/> class.
    /// </summary>
    /// <param name="path">The path of the settings file.</param>
    public JsonSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <summary>
    /// Gets the default path of the settings: the local application data folder of the user.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "settings.json");

    /// <summary>
    /// Reads the settings saved.
    /// </summary>
    /// <returns>The settings, or the default ones when the file is missing or cannot be read.</returns>
    public AppSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), Options) ?? AppSettings.Default
                : AppSettings.Default;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    /// <returns><see langword="true"/> if they were saved; otherwise <see langword="false"/>.</returns>
    public bool TrySave(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
