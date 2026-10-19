using PasteFlow.Core.Candidates;
using PasteFlow.Core.Matching;

namespace PasteFlow.Core.Tests.Matching;

public sealed class MatchReasonsTests
{
    [Fact]
    public void Quote_ShowsAnOrdinaryValueWhole()
    {
        Assert.Equal("« antoine.b@orange.fr »", MatchReasons.Quote(Candidate("antoine.b@orange.fr")));
    }

    [Fact]
    public void Quote_HidesASensitiveValueButItsEnd()
    {
        var iban = Candidate("FR76 3000 6000 0112 3456 7890 189") with { IsSensitive = true };

        Assert.Equal("« •••• 0189 »", MatchReasons.Quote(iban));
    }

    [Fact]
    public void Quote_ShortensALongValue()
    {
        var quoted = MatchReasons.Quote(Candidate(new string('a', 80)));

        Assert.Equal(MatchReasons.MaxQuotedLength + 4, quoted.Length);
        Assert.EndsWith("… »", quoted, StringComparison.Ordinal);
    }

    [Fact]
    public void PostalCodeAndCommune_NamesBoth()
    {
        Assert.Equal("51500 ne correspond pas à Reims selon La Poste.", MatchReasons.PostalCodeAndCommune(" 51500", "Reims "));
    }

    private static Candidate Candidate(string value)
    {
        return new Candidate { Value = value, Kind = CandidateKind.Text, Label = string.Empty, SourceName = "Test", SourceRank = 0 };
    }
}
