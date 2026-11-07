using System.IO;
using PasteFlow.App.Forms;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Tests.Bench;

/// <summary>
/// Writes in fields of a real browser, one of which refuses what it is given: the date picker of a web page does
/// not take a date written through UI Automation.
/// </summary>
public sealed class BrowserFieldWritingTests : IDisposable
{
    private const string Page = """
        <!doctype html><meta charset="utf-8"><title>Champs exigeants</title>
        <label for="effet">Date de prise d'effet</label><input id="effet" type="date">
        <label for="ville">Ville</label><input id="ville" type="text">
        """;

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pasteflow-champs-{Guid.NewGuid():N}.html");

    [SkippableFact]
    public async Task TryWriteAsync_FieldThatRefusesTheValue_IsNotSaidWritten()
    {
        Skip.If(BrowserWindow.BrowserPath is null, "Aucun navigateur Edge ou Chrome sur cette machine.");
        using var form = Open();
        var formAccess = new UiAutomationFormAccess();
        var field = await FieldAsync(formAccess, form, "Date de prise d'effet");

        // The date picker stayed empty while the fill said it was filled
        var isWritten = await formAccess.TryWriteAsync(field, "01/12/2026", CancellationToken.None);

        Assert.False(isWritten);
    }

    [SkippableFact]
    public async Task TryWriteAsync_FieldThatKeepsTheValue_IsSaidWritten()
    {
        Skip.If(BrowserWindow.BrowserPath is null, "Aucun navigateur Edge ou Chrome sur cette machine.");
        using var form = Open();
        var formAccess = new UiAutomationFormAccess();
        var field = await FieldAsync(formAccess, form, "Ville");

        var isWritten = await formAccess.TryWriteAsync(field, "Reims", CancellationToken.None);

        Assert.True(isWritten);
        Assert.Equal("Reims", form.ValueOf("Ville"));
    }

    public void Dispose()
    {
        File.Delete(_file);
    }

    private static async Task<FormField> FieldAsync(UiAutomationFormAccess formAccess, BrowserWindow form, string label)
    {
        var fields = await formAccess.ReadFormAsync(form.Handle, CancellationToken.None);
        return Assert.Single(fields, field => field.Label == label);
    }

    private BrowserWindow Open()
    {
        File.WriteAllText(_file, Page);
        return BrowserWindow.Open(new Uri(_file), "Ville");
    }
}
