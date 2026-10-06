using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;

namespace PasteFlow.Core.Tests.Matching;

public sealed class FieldCluesTests
{
    [Fact]
    public void Of_LabelTellsTheExpectedKindFirst()
    {
        var clues = FieldClues.Of(Field(label: "Téléphone", technicalName: "email"));

        Assert.Equal(new[] { CandidateKind.Phone }, clues.ExpectedKinds);
    }

    [Theory]
    [InlineData("billing_email")]
    [InlineData("billingEmail")]
    [InlineData("billing-email")]
    [InlineData("EMAIL")]
    public void Of_TechnicalNameTellsTheKindWhenLabelIsVague(string technicalName)
    {
        var clues = FieldClues.Of(Field(label: "Contact", technicalName: technicalName));

        Assert.Equal(new[] { CandidateKind.Email }, clues.ExpectedKinds);
    }

    [Theory]
    [InlineData("ex : jean.dupont@exemple.fr", CandidateKind.Email)]
    [InlineData("06 00 00 00 00", CandidateKind.Phone)]
    [InlineData("JJ/MM/AAAA", CandidateKind.Date)]
    [InlineData("dd/mm/yyyy", CandidateKind.Date)]
    [InlineData("Votre numéro de commande", CandidateKind.Reference)]
    public void Of_HintTellsTheKindWhenLabelAndTechnicalNameAreVague(string hint, CandidateKind kind)
    {
        var clues = FieldClues.Of(Field(label: "Identifiant", technicalName: "input_27", hint: hint));

        Assert.Contains(kind, clues.ExpectedKinds);
    }

    [Fact]
    public void Of_WithoutAnyClue_ExpectsNothing()
    {
        var clues = FieldClues.Of(Field(label: string.Empty));

        Assert.Empty(clues.ExpectedKinds);
        Assert.Equal(0, clues.Similarity(LabelConcepts.From("Email")));
    }

    [Fact]
    public void Similarity_UsesTheBestMatchingClue()
    {
        var clues = FieldClues.Of(Field(label: "Contact", technicalName: "lastName"));

        Assert.Equal(1.0, clues.Similarity(LabelConcepts.From("Nom")), precision: 6);
        Assert.Equal(1.0, clues.Similarity(LabelConcepts.From("Contact")), precision: 6);
    }

    private static FormField Field(string label, string technicalName = "", string hint = "")
    {
        return new FormField
        {
            Id = "field",
            Label = label,
            TechnicalName = technicalName,
            Hint = hint,
            Kind = FieldKind.Text,
            HasValue = false,
        };
    }
}
