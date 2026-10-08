using System.IO;
using PasteFlow.App.Settings;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Settings;

namespace PasteFlow.App.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pasteflow-settings-").FullName;

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    [Fact]
    public void SavedSettings_AreReadBack()
    {
        var settings = new AppSettings { Prudence = Prudence.Cautious, LearnFromFills = false, StartWithWindows = true };

        Assert.True(new JsonSettingsStore(SettingsPath).TrySave(settings));

        Assert.Equal(settings, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void SavedSettings_AreReadableByAPerson()
    {
        _ = new JsonSettingsStore(SettingsPath).TrySave(new AppSettings { Prudence = Prudence.Bold });

        Assert.Contains("\"Bold\"", File.ReadAllText(SettingsPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WithoutFile_GivesTheDefaultSettings()
    {
        Assert.Equal(AppSettings.Default, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Load_WithUnreadableFile_GivesTheDefaultSettings()
    {
        File.WriteAllText(SettingsPath, "{ not json");

        Assert.Equal(AppSettings.Default, new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void TrySave_CreatesTheFolder()
    {
        var path = Path.Combine(_folder, "sub", "settings.json");

        Assert.True(new JsonSettingsStore(path).TrySave(AppSettings.Default));
        Assert.True(File.Exists(path));
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
