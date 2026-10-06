using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Undo;

public sealed class UndoRulesTests
{
    [Theory]
    [InlineData("antoine.b@orange.fr", "antoine.b@orange.fr")]
    [InlineData("06 47 21 85 39", "0647218539")]
    [InlineData("Antoine.B@orange.fr", "antoine.b@orange.fr")]
    [InlineData("Reims", " Reims ")]
    public void IsStillWritten_SameValueEvenReformatted_IsTrue(string written, string current)
    {
        Assert.True(UndoRules.IsStillWritten(written, current));
    }

    [Theory]
    [InlineData("antoine.b@orange.fr", "antoine.barthelemy@orange.fr")]
    [InlineData("Reims", "")]
    [InlineData("Reims", "Reims centre")]
    public void IsStillWritten_ValueChangedByUser_IsFalse(string written, string current)
    {
        Assert.False(UndoRules.IsStillWritten(written, current));
    }
}
