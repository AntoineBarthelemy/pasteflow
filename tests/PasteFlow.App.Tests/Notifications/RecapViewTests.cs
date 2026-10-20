using PasteFlow.App.Notifications;
using PasteFlow.App.Screen;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Tests.Notifications;

public sealed class RecapViewTests
{
    [Fact]
    public void Create_ShowsTitleLinesUndoHintAndDetails()
    {
        var recap = Recap() with
        {
            Lines = new[]
            {
                new RecapLine { Kind = RecapLineKind.ToCheck, Label = "Commentaire", Value = "à vérifier" },
                new RecapLine { Kind = RecapLineKind.Filled, Label = "Email", Value = "antoine.b@orange.fr", SourceName = "Gmail" },
            },
            HiddenLineCount = 3,
            UndoHint = "Ctrl+Alt+Z pour tout annuler",
        };

        var text = ReadPopupText(recap);

        Assert.Contains("1 champ rempli, 1 à vérifier", text, StringComparison.Ordinal);
        Assert.Contains("120 ms", text, StringComparison.Ordinal);
        Assert.Contains("Commentaire", text, StringComparison.Ordinal);
        Assert.Contains("à vérifier", text, StringComparison.Ordinal);
        Assert.Contains("antoine.b@orange.fr", text, StringComparison.Ordinal);
        Assert.Contains("Gmail", text, StringComparison.Ordinal);
        Assert.Contains("+ 3 autres champs", text, StringComparison.Ordinal);
        Assert.Contains("Ctrl+Alt+Z", text, StringComparison.Ordinal);
        Assert.Contains("tout annuler", text, StringComparison.Ordinal);
        Assert.Contains("Lu à l'écran : Gmail (12).", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithOneHiddenLine_UsesSingular()
    {
        var text = ReadPopupText(Recap() with { HiddenLineCount = 1 });

        Assert.Contains("+ 1 autre champ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WithoutUndoHint_DoesNotMentionUndo()
    {
        var text = ReadPopupText(Recap());

        Assert.DoesNotContain("annuler", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 4000)]
    [InlineData(2, 5400)]
    [InlineData(FillRecap.MaxLines, 9600)]
    public void DisplayTimeFor_GrowsWithTheNumberOfLines(int lineCount, int expectedMilliseconds)
    {
        var lines = Enumerable.Range(0, lineCount)
            .Select(_ => new RecapLine { Kind = RecapLineKind.Filled, Label = "Champ", Value = "valeur" })
            .ToList();

        var displayTime = RecapView.DisplayTimeFor(Recap() with { Lines = lines });

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), displayTime);
    }

    private static string ReadPopupText(FillRecap recap)
    {
        using var host = new WindowHost<NotificationPopup>(
            () => new NotificationPopup(RecapView.Create(recap), TimeSpan.FromMinutes(1)));

        var windowText = UiAutomationWindowTextReader.ReadWindow(host.Handle, rank: 0);

        Assert.NotNull(windowText);
        return windowText.Text;
    }

    private static FillRecap Recap()
    {
        return new FillRecap
        {
            Title = "1 champ rempli, 1 à vérifier",
            Duration = "120 ms",
            Lines = Array.Empty<RecapLine>(),
            Details = new[] { "Lu à l'écran : Gmail (12)." },
        };
    }
}
