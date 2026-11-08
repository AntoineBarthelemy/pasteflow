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
        <label for="pays">Pays</label><select id="pays"><option value="">Choisir…</option><option>France</option><option>Belgique</option></select>
        <fieldset><legend>Type de logement</legend><label><input type="radio" name="type" value="Maison"> Maison</label><label><input type="radio" name="type" value="Appartement"> Appartement</label></fieldset>
        <label><input type="checkbox" id="meuble"> Logement meublé</label>
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

    [SkippableFact]
    public async Task ReadFormAsync_ListsRadioButtonsAndTickBoxes_AreReadAsChoices()
    {
        Skip.If(BrowserWindow.BrowserPath is null, "Aucun navigateur Edge ou Chrome sur cette machine.");
        using var form = Open();
        var formAccess = new UiAutomationFormAccess();

        var list = await FieldAsync(formAccess, form, "Pays");
        var group = await FieldAsync(formAccess, form, "Type de logement");
        var box = await FieldAsync(formAccess, form, "Logement meublé");

        // A closed list of a web page does not give its options; its invite « Choisir… » tells it is still empty
        Assert.Equal(FieldKind.Choice, list.Kind);
        Assert.False(list.HasValue);
        Assert.Equal(FieldKind.Choice, group.Kind);
        Assert.Equal(new[] { "Maison", "Appartement" }, group.Options);
        Assert.Equal(FieldKind.CheckBox, box.Kind);
    }

    [SkippableFact]
    public async Task TryWriteAsync_ChoosesTheOptionOfAListAGroupAndATickBox()
    {
        Skip.If(BrowserWindow.BrowserPath is null, "Aucun navigateur Edge ou Chrome sur cette machine.");
        using var form = Open();
        var formAccess = new UiAutomationFormAccess();

        Assert.True(await formAccess.TryWriteAsync(await FieldAsync(formAccess, form, "Pays"), "Belgique", CancellationToken.None));
        Assert.True(await formAccess.TryWriteAsync(await FieldAsync(formAccess, form, "Type de logement"), "Appartement", CancellationToken.None));
        Assert.True(await formAccess.TryWriteAsync(await FieldAsync(formAccess, form, "Logement meublé"), "oui", CancellationToken.None));

        Assert.True((await FieldAsync(formAccess, form, "Pays")).HasValue);
        Assert.True((await FieldAsync(formAccess, form, "Type de logement")).HasValue);
        Assert.True((await FieldAsync(formAccess, form, "Logement meublé")).HasValue);
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
