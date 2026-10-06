using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Undo;

public sealed class UndoReportTests
{
    [Fact]
    public void Describe_WithNothingToUndo_SaysSo()
    {
        Assert.Equal("Rien à annuler.", Report(emptied: 0, changed: 0, failed: 0).Describe());
    }

    [Fact]
    public void Describe_WithOneOfEach_UsesSingular()
    {
        Assert.Equal(
            "Annulé : 1 champ vidé, 1 laissé car modifié depuis, 1 impossible à vider.",
            Report(emptied: 1, changed: 1, failed: 1).Describe());
    }

    [Fact]
    public void Describe_WithSeveralOfEach_UsesPlural()
    {
        Assert.Equal(
            "Annulé : 3 champs vidés, 2 laissés car modifiés depuis, 2 impossibles à vider.",
            Report(emptied: 3, changed: 2, failed: 2).Describe());
    }

    [Fact]
    public void Describe_OnlyMentionsNonZeroCounts()
    {
        Assert.Equal("Annulé : 2 champs vidés.", Report(emptied: 2, changed: 0, failed: 0).Describe());
    }

    [Fact]
    public void Describe_WhenNoFieldCouldBeReached_SuggestsTheFormWasClosed()
    {
        Assert.Equal(
            "Annulé : 2 impossibles à vider.\nLe formulaire a sans doute été fermé.",
            Report(emptied: 0, changed: 0, failed: 2).Describe());
    }

    private static UndoReport Report(int emptied, int changed, int failed)
    {
        return new UndoReport { EmptiedCount = emptied, ChangedCount = changed, FailedCount = failed };
    }
}
