using PasteFlow.Core.Candidates;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Matching;

public sealed class MatchingOptionsTests
{
    [Fact]
    public void For_Balanced_IsTheDefault()
    {
        Assert.Same(MatchingOptions.Balanced, MatchingOptions.For(Prudence.Balanced));
        Assert.Equal(FieldMatcher.ConfidenceThreshold, MatchingOptions.Balanced.ConfidenceThreshold);
        Assert.Equal(FieldMatcher.MinimumLead, MatchingOptions.Balanced.MinimumLead);
    }

    [Fact]
    public void For_CautiousAsksMoreAndBoldLess()
    {
        var cautious = MatchingOptions.For(Prudence.Cautious);
        var bold = MatchingOptions.For(Prudence.Bold);

        Assert.True(cautious.ConfidenceThreshold > MatchingOptions.Balanced.ConfidenceThreshold);
        Assert.True(cautious.MinimumLead > MatchingOptions.Balanced.MinimumLead);
        Assert.True(bold.ConfidenceThreshold < MatchingOptions.Balanced.ConfidenceThreshold);
        Assert.True(bold.MinimumLead < MatchingOptions.Balanced.MinimumLead);
    }

    [Theory]
    [InlineData(Prudence.Cautious, false)]
    [InlineData(Prudence.Balanced, true)]
    [InlineData(Prudence.Bold, true)]
    public void MatchAll_OnlyValueOfItsKindWithoutLabel_IsFilledUnlessCautious(Prudence prudence, bool isFilled)
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("phone", "Téléphone") },
            new[] { new Candidate { Value = "06 47 21 85 39", Kind = CandidateKind.Phone, SourceName = "Gmail", SourceRank = 0 } },
            TestReferenceData.Create(),
            MatchingOptions.For(prudence));

        Assert.Equal(isFilled, matches[0].IsConfident);
    }

    [Theory]
    [InlineData(Prudence.Cautious, true)]
    [InlineData(Prudence.Balanced, true)]
    public void MatchAll_ValueWithMatchingLabel_IsFilledEvenWhenCautious(Prudence prudence, bool isFilled)
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("phone", "Téléphone") },
            new[] { new Candidate { Value = "06 47 21 85 39", Kind = CandidateKind.Phone, Label = "Tél.", SourceName = "Gmail", SourceRank = 0 } },
            TestReferenceData.Create(),
            MatchingOptions.For(prudence));

        Assert.Equal(isFilled, matches[0].IsConfident);
    }

    [Fact]
    public void MatchAll_WithBoldPrudence_StillLeavesTiesToTheUser()
    {
        var matches = FormMatcher.MatchAll(
            new[] { TestFields.Text("email", "Email") },
            new[]
            {
                new Candidate { Value = "antoine.b@orange.fr", Kind = CandidateKind.Email, SourceName = "Gmail", SourceRank = 0 },
                new Candidate { Value = "contact@reims-auto.fr", Kind = CandidateKind.Email, SourceName = "Gmail", SourceRank = 0 },
            },
            TestReferenceData.Create(),
            MatchingOptions.For(Prudence.Bold));

        Assert.False(matches[0].IsConfident);
    }
}
