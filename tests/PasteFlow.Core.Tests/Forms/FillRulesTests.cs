using PasteFlow.Core.Forms;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Forms;

public sealed class FillRulesTests
{
    [Fact]
    public void CanFill_WithEmptyTextField_ReturnsTrue()
    {
        Assert.True(FillRules.CanFill(TestFields.Text("1", "Nom")));
    }

    [Fact]
    public void CanFill_WithTextFieldFilledByUser_ReturnsFalse()
    {
        Assert.False(FillRules.CanFill(TestFields.Text("1", "Nom", hasValue: true)));
    }

    [Fact]
    public void CanFill_WithPasswordField_ReturnsFalse()
    {
        Assert.False(FillRules.CanFill(TestFields.Password("1", "Mot de passe")));
    }

    [Fact]
    public void CanFill_WithListOfChoices_ReturnsFalse()
    {
        Assert.False(FillRules.CanFill(TestFields.Choice("1", "Pays")));
    }
}
