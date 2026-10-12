using System.Text;
using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Profile;

public sealed class ProfileArchiveTests
{
    private const string Password = "vignes-de-Taissy";

    private static readonly ProfileValue[] Values =
    {
        new() { Topic = ProfileTopic.Email, Value = "antoine.b@orange.fr", Label = "Perso" },
        new() { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189", Origin = ProfileOrigin.PasswordManager },
    };

    [Fact]
    public void Export_IsReadBackWithItsPassword()
    {
        var archive = ProfileArchive.Export(Values, Password);

        var reading = ProfileArchive.Read(archive, Password);

        Assert.Equal(ProfileArchiveStatus.Read, reading.Status);
        Assert.Equal(Values, reading.Values);
    }

    [Fact]
    public void Export_IsEncrypted()
    {
        var archive = ProfileArchive.Export(Values, Password);

        var text = Encoding.UTF8.GetString(archive);
        Assert.DoesNotContain("orange", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("3000", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_TwiceGivesDifferentFiles()
    {
        Assert.NotEqual(ProfileArchive.Export(Values, Password), ProfileArchive.Export(Values, Password));
    }

    [Fact]
    public void Read_WithAnotherPassword_SaysSo()
    {
        var archive = ProfileArchive.Export(Values, Password);

        var reading = ProfileArchive.Read(archive, "vignes-de-Reims");

        Assert.Equal(ProfileArchiveStatus.WrongPassword, reading.Status);
        Assert.Empty(reading.Values);
    }

    [Fact]
    public void Read_OfAChangedFile_IsRefused()
    {
        var archive = ProfileArchive.Export(Values, Password);
        archive[^1] ^= 0x01;

        Assert.Equal(ProfileArchiveStatus.WrongPassword, ProfileArchive.Read(archive, Password).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PASTEFLOW-PROFILE-1")]
    [InlineData("Ceci est un fichier texte ordinaire, assez long pour avoir la taille d'un export de PasteFlow.")]
    public void Read_OfAnotherFile_SaysItIsNotAnExport(string content)
    {
        Assert.Equal(ProfileArchiveStatus.NotAnArchive, ProfileArchive.Read(Encoding.UTF8.GetBytes(content), Password).Status);
    }

    [Fact]
    public void Export_WithAShortPassword_IsRefused()
    {
        _ = Assert.Throws<ArgumentException>(() => ProfileArchive.Export(Values, "court"));
    }
}
