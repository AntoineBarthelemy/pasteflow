using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Forms;

public sealed class FillReportTests
{
    private static readonly TimeSpan Elapsed = TimeSpan.FromMilliseconds(120.4);

    [Fact]
    public void Describe_WithNothingToFill_SaysSo()
    {
        var report = Report(filled: 0, alreadyFilled: 0, failed: 0);

        Assert.Equal("Aucun champ à remplir dans cette fenêtre. En 120 ms.", report.Describe());
    }

    [Fact]
    public void Describe_WithOneOfEach_UsesSingular()
    {
        var report = Report(filled: 1, alreadyFilled: 1, failed: 1);

        Assert.Equal(
            "1 champ rempli, 1 déjà rempli laissé intact, 1 champ impossible à remplir. En 120 ms.",
            report.Describe());
    }

    [Fact]
    public void Describe_WithSeveralOfEach_UsesPlural()
    {
        var report = Report(filled: 3, alreadyFilled: 2, failed: 2);

        Assert.Equal(
            "3 champs remplis, 2 déjà remplis laissés intacts, 2 champs impossibles à remplir. En 120 ms.",
            report.Describe());
    }

    [Fact]
    public void Describe_OnlyMentionsNonZeroCounts()
    {
        var report = Report(filled: 2, alreadyFilled: 0, failed: 0);

        Assert.Equal("2 champs remplis. En 120 ms.", report.Describe());
    }

    private static FillReport Report(int filled, int alreadyFilled, int failed)
    {
        return new FillReport
        {
            FilledCount = filled,
            AlreadyFilledCount = alreadyFilled,
            FailedCount = failed,
            Elapsed = Elapsed,
        };
    }
}
