using PasteFlow.App.Diagnostics;

namespace PasteFlow.App.Tests.Diagnostics;

public sealed class ErrorLogTests
{
    [Fact]
    public void Describe_KeepsTheKindOfErrorButNeverItsMessage()
    {
        var error = new InvalidOperationException("antoine.b@orange.fr", new ArgumentException("FR76 3000"));

        var description = ErrorLog.Describe(error);

        Assert.Contains("System.InvalidOperationException", description, StringComparison.Ordinal);
        Assert.Contains("System.ArgumentException", description, StringComparison.Ordinal);
        Assert.DoesNotContain("orange", description, StringComparison.Ordinal);
        Assert.DoesNotContain("3000", description, StringComparison.Ordinal);
    }

    [Fact]
    public void Recent_KeepsTheLastWholeErrors()
    {
        var first = "2026-10-20 21:00:00 Erreur inattendue\nSystem.ArgumentException (0x80070057)\n   at Premier()\n";
        var second = "2026-10-20 22:00:00 Tâche en arrière-plan\nSystem.InvalidOperationException (0x80131509)\n   at Second()\n";

        var recent = ErrorLog.Recent(first + second, second.Length + 10);

        Assert.Equal(second, recent);
    }

    [Fact]
    public void Recent_KeepsAShortFileWhole()
    {
        const string text = "2026-10-20 21:00:00 Erreur inattendue\nSystem.ArgumentException (0x80070057)\n";

        Assert.Equal(text, ErrorLog.Recent(text, 1000));
    }
}
