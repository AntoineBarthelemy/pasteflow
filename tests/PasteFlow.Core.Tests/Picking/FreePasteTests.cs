using PasteFlow.Core.Forms;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Picking;

public sealed class FreePasteTests
{
    [Fact]
    public void IsFreeText_InADocumentWithoutField_ReturnsTrue()
    {
        Assert.True(FreePaste.IsFreeText(Array.Empty<FormField>(), new CaretContext { IsEditable = true }));
    }

    [Fact]
    public void IsFreeText_InTheOnlyTextField_ReturnsTrue()
    {
        var fields = new[] { TestFields.Text("message", "Message"), TestFields.Text("search", "Rechercher") };

        Assert.True(FreePaste.IsFreeText(fields, new CaretContext { IsEditable = true, FocusedFieldId = "message" }));
    }

    [Fact]
    public void IsFreeText_InAFieldOfAForm_ReturnsFalse()
    {
        var fields = new[] { TestFields.Text("name", "Nom"), TestFields.Text("email", "Email") };

        Assert.False(FreePaste.IsFreeText(fields, new CaretContext { IsEditable = true, FocusedFieldId = "email" }));
    }

    [Fact]
    public void IsFreeText_InThePageOfAWordProcessorWithItsToolbarBoxes_ReturnsTrue()
    {
        // Seen on 14/11/2026 in WordPad: the font and size boxes made the page look like a field of a form
        var fields = new[] { TestFields.Text("page", string.Empty, hasValue: true), TestFields.Text("font", "Famille de polices", hasValue: true), TestFields.Text("size", "Taille de police", hasValue: true) };
        var caret = new CaretContext { IsEditable = true, FocusedFieldId = "page", TextBefore = "Commande : ", IsDocument = true };

        Assert.True(FreePaste.IsFreeText(fields, caret));
    }

    [Fact]
    public void IsFreeText_InTheGridOfASpreadsheet_PastesAtTheCursorDespiteTheRibbonBoxes()
    {
        // Seen on 14/11/2026 in Excel: the cell is no text box, and the name box, formula bar and font boxes of the
        // ribbon made the window look like a form where nothing was pasted
        var fields = new[] { TestFields.Text("font", "Police"), TestFields.Text("name", "Zone Nom"), TestFields.Text("formula", "Barre de formule") };
        var caret = new CaretContext { IsEditable = false, IsDocument = true };

        Assert.True(FreePaste.IsFreeText(fields, caret));
    }

    [Fact]
    public void IsFreeText_OutsideAnyTextBoxOrDocument_FillsTheForm()
    {
        var fields = new[] { TestFields.Text("first", "Prénom"), TestFields.Text("last", "Nom") };

        Assert.False(FreePaste.IsFreeText(fields, new CaretContext { IsEditable = false }));
    }

    [Fact]
    public void IsFreeText_InALargeBoxOfASignInPage_StillFillsTheForm()
    {
        var fields = new[] { TestFields.Text("login", "Identifiant"), TestFields.Password("password", "Mot de passe") };

        Assert.False(FreePaste.IsFreeText(fields, new CaretContext { IsEditable = true, FocusedFieldId = "login", IsDocument = true }));
    }

    [Fact]
    public void IsFreeText_WhenASentenceIsBeingWrittenInABoxAmongOthers_ReturnsTrue()
    {
        var fields = new[] { TestFields.Text("message", "Message au client"), TestFields.Text("note", "Note interne") };
        var caret = new CaretContext { IsEditable = true, FocusedFieldId = "message", TextBefore = "Bonjour, voici son numéro de suivi : " };

        Assert.True(FreePaste.IsFreeText(fields, caret));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Mar")]
    [InlineData("Marie Lefèvre")]
    [InlineData("14 avenue Jean")]
    public void IsFreeText_WhenTheBoxHoldsAShortValue_StillFillsTheForm(string textBefore)
    {
        var fields = new[] { TestFields.Text("name", "Nom"), TestFields.Text("address", "Adresse") };
        var caret = new CaretContext { IsEditable = true, FocusedFieldId = "name", TextBefore = textBefore };

        Assert.False(FreePaste.IsFreeText(fields, caret));
    }

    [Fact]
    public void IsFreeText_OnASignInPage_NeverPastesAtTheCursor()
    {
        var fields = new[] { TestFields.Text("username", "Identifiant"), TestFields.Password("password", "Mot de passe") };
        var caret = new CaretContext { IsEditable = true, FocusedFieldId = "username", TextBefore = "mon nom de compte habituel" };

        Assert.False(FreePaste.IsFreeText(fields, caret));
    }

    [Fact]
    public void IsFreeText_InTheIdentifierOfASignInPage_ReturnsFalse()
    {
        var fields = new[] { TestFields.Text("username", "Username"), TestFields.Password("password", "Password") };

        Assert.False(FreePaste.IsFreeText(fields, new CaretContext { IsEditable = true, FocusedFieldId = "username" }));
    }

    [Fact]
    public void IsFreeText_InTheBodyOfAMessageNextToItsFields_ReturnsTrue()
    {
        var fields = new[] { TestFields.Text("to", "À"), TestFields.Text("subject", "Objet") };

        Assert.True(FreePaste.IsFreeText(fields, new CaretContext { IsEditable = true }));
    }

    [Fact]
    public void IsFreeText_WhereNothingCanBeTyped_ReturnsFalse()
    {
        Assert.False(FreePaste.IsFreeText(Array.Empty<FormField>(), new CaretContext { IsEditable = false }));
    }

    [Theory]
    [InlineData("Bonjour Paul,\nTiens, mon mail : ", "Tiens, mon mail :")]
    [InlineData("", "")]
    public void TargetField_IsLabelledByTheCurrentLine(string textBefore, string label)
    {
        var field = FreePaste.TargetField(new CaretContext { IsEditable = true, TextBefore = textBefore, FieldLabel = "Message" });

        Assert.Equal(label, field.Label);
        Assert.Equal("Message", field.Hint);
        Assert.False(field.HasValue);
    }

    [Fact]
    public void TargetField_KeepsOnlyTheEndOfALongLine()
    {
        var textBefore = new string('x', 100) + " et voici mon téléphone : ";

        var field = FreePaste.TargetField(new CaretContext { IsEditable = true, TextBefore = textBefore });

        Assert.True(field.Label.Length <= FreePaste.MaxLabelLength);
        Assert.EndsWith("voici mon téléphone :", field.Label, StringComparison.Ordinal);
    }
}
