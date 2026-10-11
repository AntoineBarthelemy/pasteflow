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

    [Theory]
    [InlineData("Search across your channel", "", "")]
    [InlineData("Rechercher", "", "")]
    [InlineData("", "search_query", "")]
    [InlineData("", "", "Rechercher un produit")]
    [InlineData("Filtrer les résultats", "", "")]
    public void CanFill_WithSearchBox_ReturnsFalse(string label, string technicalName, string hint)
    {
        var field = TestFields.Text("search", label) with { TechnicalName = technicalName, Hint = hint };

        Assert.False(FillRules.CanFill(field));
        Assert.True(FillRules.IsSearchBox(field));
    }

    [Theory]
    [InlineData("Title (required)")]
    [InlineData("Description")]
    [InlineData("Adresse e-mail")]
    public void IsSearchBox_WithOrdinaryField_ReturnsFalse(string label)
    {
        Assert.False(FillRules.IsSearchBox(TestFields.Text("field", label)));
    }
}
