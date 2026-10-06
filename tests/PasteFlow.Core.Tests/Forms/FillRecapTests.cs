using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Forms;

public sealed class FillRecapTests
{
    [Fact]
    public void From_ListsFieldsToCheckFirstThenFilledFields()
    {
        var phone = TestFields.Text("phone", "Téléphone");
        var misc = TestFields.Text("misc", "Commentaire");
        var report = Report() with
        {
            FilledCount = 1,
            UncertainCount = 1,
            FailedCount = 1,
            FilledValues = new[] { Filled("Email", "antoine.b@orange.fr", "Gmail") },
            FieldsToCheck = new[] { phone, misc },
            FailedFields = new[] { phone },
        };

        var recap = FillRecap.From(report);

        Assert.Equal(
            new[]
            {
                new RecapLine { Kind = RecapLineKind.Failed, Label = "Téléphone", Value = "non écrit" },
                new RecapLine { Kind = RecapLineKind.ToCheck, Label = "Commentaire", Value = "à vérifier" },
                new RecapLine { Kind = RecapLineKind.Filled, Label = "Email", Value = "antoine.b@orange.fr", SourceName = "Gmail" },
            },
            recap.Lines);
    }

    [Fact]
    public void From_SumsTheResultUpInTheTitle()
    {
        var report = Report() with { FilledCount = 2, UncertainCount = 1, Elapsed = TimeSpan.FromMilliseconds(120.4) };

        var recap = FillRecap.From(report);

        Assert.Equal("2 champs remplis, 1 à vérifier", recap.Title);
        Assert.Equal("120 ms", recap.Duration);
    }

    [Fact]
    public void From_WithNothingToFill_SaysSo()
    {
        var recap = FillRecap.From(Report());

        Assert.Equal("Aucun champ à remplir dans cette fenêtre", recap.Title);
        Assert.Empty(recap.Lines);
        Assert.Equal(new[] { "Rien lu à l'écran." }, recap.Details);
    }

    [Fact]
    public void From_ListsOnlyTheFirstFieldsAndCountsTheOthers()
    {
        var filled = Enumerable.Range(1, FillRecap.MaxLines + 3)
            .Select(index => Filled($"Champ {index}", $"valeur {index}", "Gmail"))
            .ToList();
        var report = Report() with { FilledCount = filled.Count, FilledValues = filled };

        var recap = FillRecap.From(report);

        Assert.Equal(FillRecap.MaxLines, recap.Lines.Count);
        Assert.Equal(3, recap.HiddenLineCount);
        Assert.Equal("Champ 1", recap.Lines[0].Label);
    }

    [Fact]
    public void From_ShortensLongTextsAndNamesUnnamedFields()
    {
        var longValue = new string('a', FillRecap.MaxTextLength + 10);
        var report = Report() with
        {
            FilledCount = 1,
            FilledValues = new[] { Filled(" ", longValue, new string('b', FillRecap.MaxTextLength + 10)) },
        };

        var line = Assert.Single(FillRecap.From(report).Lines);

        Assert.Equal("Champ sans nom", line.Label);
        Assert.Equal(FillRecap.MaxTextLength, line.Value.Length);
        Assert.EndsWith("…", line.Value, StringComparison.Ordinal);
        Assert.Equal(FillRecap.MaxTextLength, line.SourceName.Length);
    }

    [Fact]
    public void From_AfterFilling_GivesTheUndoShortcut()
    {
        var report = Report() with
        {
            FilledCount = 1,
            FilledValues = new[] { Filled("Ville", "Reims", "Gmail") },
            UndoHotkey = Hotkey.DefaultUndo,
        };

        Assert.Equal("Ctrl+Alt+Z pour tout annuler", FillRecap.From(report).UndoHint);
    }

    [Fact]
    public void From_WithoutFilledFieldOrUndoShortcut_GivesNoUndoHint()
    {
        Assert.Null(FillRecap.From(Report() with { UndoHotkey = Hotkey.DefaultUndo }).UndoHint);
        Assert.Null(FillRecap.From(Report() with { FilledCount = 1 }).UndoHint);
    }

    [Fact]
    public void From_GivesSourcesAndIgnoredExamplesAsDetails()
    {
        var report = Report() with
        {
            Sources = new[] { new SourceSummary { Name = "Gmail", CandidateCount = 12 } },
            IgnoredSampleCount = 2,
        };

        var recap = FillRecap.From(report);

        Assert.Equal(new[] { "Lu à l'écran : Gmail (12).", "Ignoré : 2 exemples." }, recap.Details);
    }

    private static FillReport Report()
    {
        return new FillReport
        {
            FilledCount = 0,
            UncertainCount = 0,
            AlreadyFilledCount = 0,
            FailedCount = 0,
            Sources = Array.Empty<SourceSummary>(),
            Elapsed = TimeSpan.FromMilliseconds(80),
        };
    }

    private static FilledValue Filled(string label, string value, string source)
    {
        return new FilledValue { FieldLabel = label, Value = value, SourceName = source };
    }
}
