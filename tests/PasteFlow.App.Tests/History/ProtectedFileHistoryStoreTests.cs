using System.Text;
using PasteFlow.App.History;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.History;

namespace PasteFlow.App.Tests.History;

public sealed class ProtectedFileHistoryStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pasteflow-tests-").FullName;

    private string HistoryPath => Path.Combine(_folder, "history.dat");

    [Fact]
    public async Task SavedHistory_IsReadBack()
    {
        var store = new ProtectedFileHistoryStore(HistoryPath);
        var entries = new[] { Entry("Email", "antoine.b@orange.fr", CandidateKind.Email), Entry("Ville", "Taissy", CandidateKind.City) };

        Assert.True(await store.TrySaveAsync(entries, CancellationToken.None));

        Assert.Equal(entries, await new ProtectedFileHistoryStore(HistoryPath).LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SavedHistory_IsEncrypted()
    {
        var store = new ProtectedFileHistoryStore(HistoryPath);

        _ = await store.TrySaveAsync(new[] { Entry("Email", "antoine.b@orange.fr", CandidateKind.Email) }, CancellationToken.None);

        var content = await File.ReadAllBytesAsync(HistoryPath);
        Assert.DoesNotContain("antoine", Encoding.UTF8.GetString(content), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("antoine", Encoding.Unicode.GetString(content), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_WithoutFile_GivesAnEmptyHistory()
    {
        Assert.Empty(await new ProtectedFileHistoryStore(HistoryPath).LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_WithUnreadableFile_SetsItAsideAndStartsEmpty()
    {
        await File.WriteAllTextAsync(HistoryPath, "not an encrypted history");

        var entries = await new ProtectedFileHistoryStore(HistoryPath).LoadAsync(CancellationToken.None);

        Assert.Empty(entries);
        Assert.False(File.Exists(HistoryPath));
        Assert.True(File.Exists(HistoryPath + ".unreadable"));
    }

    [Fact]
    public async Task TrySaveAsync_CreatesTheFolderAndReplacesThePreviousHistory()
    {
        var path = Path.Combine(_folder, "sub", "history.dat");
        var store = new ProtectedFileHistoryStore(path);

        _ = await store.TrySaveAsync(new[] { Entry("Email", "antoine.b@orange.fr", CandidateKind.Email) }, CancellationToken.None);
        _ = await store.TrySaveAsync(Array.Empty<HistoryEntry>(), CancellationToken.None);

        Assert.Empty(await store.LoadAsync(CancellationToken.None));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void DefaultPath_IsInTheLocalDataOfTheUser()
    {
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProtectedFileHistoryStore.DefaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("PasteFlow", "history.dat"), ProtectedFileHistoryStore.DefaultPath, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static HistoryEntry Entry(string label, string value, CandidateKind kind)
    {
        return new HistoryEntry
        {
            FieldLabel = label,
            Value = value,
            Kind = kind,
            UseCount = 2,
            LastUsed = new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero),
        };
    }
}
