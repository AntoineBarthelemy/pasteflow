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
