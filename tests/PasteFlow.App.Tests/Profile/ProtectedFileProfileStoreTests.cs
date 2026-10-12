using System.IO;
using System.Text;
using PasteFlow.App.History;
using PasteFlow.App.Profile;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Tests.Profile;

public sealed class ProtectedFileProfileStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pasteflow-tests-").FullName;

    private string ProfilePath => Path.Combine(_folder, "profile.dat");

    [Fact]
    public async Task SavedProfile_IsReadBack()
    {
        var store = new ProtectedFileProfileStore(ProfilePath);
        var values = new[]
        {
            new ProfileValue { Topic = ProfileTopic.Email, Value = "antoine.b@orange.fr", Label = "Perso", AddedAt = new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero) },
            new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189", Origin = ProfileOrigin.PasswordManager },
        };

        Assert.True(await store.TrySaveAsync(values, CancellationToken.None));

        Assert.Equal(values, await new ProtectedFileProfileStore(ProfilePath).LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SavedProfile_IsEncrypted()
    {
        var store = new ProtectedFileProfileStore(ProfilePath);

        _ = await store.TrySaveAsync(new[] { new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189" } }, CancellationToken.None);

        var content = await File.ReadAllBytesAsync(ProfilePath);
        Assert.DoesNotContain("FR76", Encoding.UTF8.GetString(content), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FR76", Encoding.Unicode.GetString(content), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProfileFile_CannotBeReadAsAHistory()
    {
        _ = await new ProtectedFileProfileStore(ProfilePath).TrySaveAsync(new[] { new ProfileValue { Topic = ProfileTopic.City, Value = "Taissy" } }, CancellationToken.None);

        Assert.Empty(await new ProtectedFileHistoryStore(ProfilePath).LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_WithUnreadableFile_SetsItAsideAndStartsEmpty()
    {
        await File.WriteAllTextAsync(ProfilePath, "not an encrypted profile");

        Assert.Empty(await new ProtectedFileProfileStore(ProfilePath).LoadAsync(CancellationToken.None));
        Assert.True(File.Exists(ProfilePath + ".unreadable"));
    }

    [Fact]
    public void DefaultPath_IsInTheLocalDataOfTheUser()
    {
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProtectedFileProfileStore.DefaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("PasteFlow", "profile.dat"), ProtectedFileProfileStore.DefaultPath, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
