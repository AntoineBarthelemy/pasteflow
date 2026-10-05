using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Forms;

public sealed class FillReportTests
{
    private static readonly TimeSpan Elapsed = TimeSpan.FromMilliseconds(120.4);

    [Fact]
    public void Describe_WithNothingToFill_SaysSo()
    {
        var report = Report(filled: 0, alreadyFilled: 0, failed: 0);

        Assert.Equal("Aucun champ à remplir dans cette fenêtre. En 120 ms.\nRien lu à l'écran.", report.Describe());
    }

    [Fact]
    public void Describe_WithOneOfEach_UsesSingular()
    {
        var report = Report(filled: 1, alreadyFilled: 1, failed: 1);

        Assert.Equal(
            "1 champ rempli, 1 déjà rempli laissé intact, 1 champ impossible à remplir. En 120 ms.\nRien lu à l'écran.",
            report.Describe());
    }

    [Fact]
    public void Describe_WithSeveralOfEach_UsesPlural()
    {
        var report = Report(filled: 3, alreadyFilled: 2, failed: 2);

        Assert.Equal(
            "3 champs remplis, 2 déjà remplis laissés intacts, 2 champs impossibles à remplir. En 120 ms.\nRien lu à l'écran.",
            report.Describe());
    }

    [Fact]
    public void Describe_OnlyMentionsNonZeroCounts()
    {
        var report = Report(filled: 2, alreadyFilled: 0, failed: 0);

        Assert.Equal("2 champs remplis. En 120 ms.\nRien lu à l'écran.", report.Describe());
    }

    [Fact]
    public void Describe_ListsSourcesWithTheirCandidateCount()
    {
        var sources = new[] { Source("Gmail", 84), Source("Vide", 0), Source(string.Empty, 3) };
        var report = Report(filled: 1, alreadyFilled: 0, failed: 0, sources);

        Assert.EndsWith("\nLu à l'écran : Gmail (84), sans titre (3).", report.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_ShortensLongSourceNamesAndListsFewSources()
    {
        var longName = new string('a', FillReport.MaxSourceNameLength + 10);
        var sources = Enumerable.Range(0, FillReport.MaxListedSources + 1)
            .Select(index => Source(index == 0 ? longName : $"Fenêtre {index}", 1))
            .ToArray();
        var report = Report(filled: 1, alreadyFilled: 0, failed: 0, sources);

        var message = report.Describe();

        Assert.Contains($"{new string('a', FillReport.MaxSourceNameLength - 1)}… (1)", message, StringComparison.Ordinal);
        Assert.Contains("Fenêtre 3 (1).", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Fenêtre 4", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_ListsRecognizedValuesByKind()
    {
        var report = Report(filled: 1, alreadyFilled: 0, failed: 0, new[] { Source("Gmail", 10) }) with
        {
            RecognizedCounts = new Dictionary<CandidateKind, int>
            {
                [CandidateKind.Reference] = 3,
                [CandidateKind.Email] = 1,
                [CandidateKind.Phone] = 2,
                [CandidateKind.Text] = 40,
                [CandidateKind.Date] = 0,
            },
        };

        Assert.EndsWith(
            "\nLu à l'écran : Gmail (10).\nReconnu : 1 e-mail, 2 téléphones, 3 références.",
            report.Describe(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WithoutRecognizedValue_HasNoRecognizedLine()
    {
        var report = Report(filled: 1, alreadyFilled: 0, failed: 0, new[] { Source("Gmail", 10) });

        Assert.DoesNotContain("Reconnu", report.Describe(), StringComparison.Ordinal);
    }

    private static SourceSummary Source(string name, int candidateCount)
    {
        return new SourceSummary { Name = name, CandidateCount = candidateCount };
    }

    private static FillReport Report(int filled, int alreadyFilled, int failed, IReadOnlyList<SourceSummary>? sources = null)
    {
        return new FillReport
        {
            FilledCount = filled,
            AlreadyFilledCount = alreadyFilled,
            FailedCount = failed,
            Sources = sources ?? Array.Empty<SourceSummary>(),
            Elapsed = Elapsed,
        };
    }
}
