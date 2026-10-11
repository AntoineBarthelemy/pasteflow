using System.Windows.Automation;
using PasteFlow.App.Forms;
using PasteFlow.App.Tests.TestWindows;

namespace PasteFlow.App.Tests.Forms;

public sealed class CaretReadingTests : IDisposable
{
    private readonly WindowHost<ContactFormWindow> _host = new(() => new ContactFormWindow());
    private readonly UiAutomationFormAccess _formAccess = new();

    [Fact]
    public async Task DescribeCaret_InATextBox_GivesItsFieldLabelAndTheTextBeforeTheCursor()
    {
        _ = _host.Invoke(window =>
        {
            window.NameBox.Text = "Bonjour, mon mail : ";
            window.NameBox.CaretIndex = window.NameBox.Text.Length;
            return true;
        });
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        var caret = _formAccess.DescribeCaret(Element("Nom"));

        Assert.True(caret.IsEditable);
        Assert.Equal("Nom", caret.FieldLabel);
        Assert.Equal(fields.Single(field => field.Label == "Nom").Id, caret.FocusedFieldId);
        Assert.Equal("Bonjour, mon mail :", caret.TextBefore.Trim());
    }

    [Fact]
    public void DescribeCaret_OutsideTheLastReading_HasNoFieldId()
    {
        var caret = _formAccess.DescribeCaret(Element("Email"));

        Assert.True(caret.IsEditable);
        Assert.Null(caret.FocusedFieldId);
    }

    [Fact]
    public void IsEditable_RefusesPasswordsDisabledFieldsAndListsOfChoices()
    {
        Assert.False(UiAutomationFormAccess.IsEditable(Element("Mot de passe")));
        Assert.False(UiAutomationFormAccess.IsEditable(Element("Désactivé")));
        Assert.False(UiAutomationFormAccess.IsEditable(Element("Pays")));
        Assert.True(UiAutomationFormAccess.IsEditable(Element("Recherche")));
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private AutomationElement Element(string name)
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        return window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name))
            ?? throw new InvalidOperationException($"Pas d'élément « {name} ».");
    }
}
