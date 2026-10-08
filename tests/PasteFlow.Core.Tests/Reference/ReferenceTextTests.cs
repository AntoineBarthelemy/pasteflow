using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Tests.Reference;

public sealed class ReferenceTextTests
{
    [Theory]
    [InlineData("Châlons-en-Champagne", "CHALONS EN CHAMPAGNE")]
    [InlineData("  Saint-Jean-de-Monts ", "ST JEAN DE MONTS")]
    [InlineData("Sainte-Menehould", "STE MENEHOULD")]
    [InlineData("L'Abergement-Clémenciat", "L ABERGEMENT CLEMENCIAT")]
    [InlineData("Côte d’Ivoire", "COTE D IVOIRE")]
    [InlineData("JÉRÔME", "JEROME")]
    [InlineData("", "")]
    public void Normalize_GivesOneComparableForm(string text, string expected)
    {
        Assert.Equal(expected, ReferenceText.Normalize(text));
    }

    [Fact]
    public void RemoveAccents_KeepsOtherCharacters()
    {
        Assert.Equal("Chalons-en-Champagne, 51000", ReferenceText.RemoveAccents("Châlons-en-Champagne, 51000"));
    }
}
