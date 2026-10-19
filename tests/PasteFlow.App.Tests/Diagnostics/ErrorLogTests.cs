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
}
