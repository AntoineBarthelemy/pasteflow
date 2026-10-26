using System.IO;
using PasteFlow.App.Logins;

namespace PasteFlow.App.Tests.Logins;

public sealed class BitwardenSessionStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pasteflow-tests-").FullName;

    [Fact]
    public void Save_KeepsTheSessionEncryptedForTheNextLaunch()
    {
        var path = Path.Combine(_folder, "bitwarden-session.bin");
        new BitwardenSessionStore(path).Save("cle-de-session-du-coffre");

        Assert.Equal("cle-de-session-du-coffre", new BitwardenSessionStore(path).Load());
        Assert.DoesNotContain("cle-de-session", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Delete_ForgetsTheSession()
    {
        var store = new BitwardenSessionStore(Path.Combine(_folder, "bitwarden-session.bin"));
        store.Save("cle-de-session-du-coffre");

        store.Delete();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_WithAnUnreadableFile_GivesNothing()
    {
        var path = Path.Combine(_folder, "bitwarden-session.bin");
        File.WriteAllText(path, "pas chiffré");

        Assert.Null(new BitwardenSessionStore(path).Load());
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
