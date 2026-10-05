using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Forms;

public sealed class FormSummaryTests
{
    private static readonly TimeSpan Elapsed = TimeSpan.FromMilliseconds(120.4);

    [Fact]
    public void Describe_WithoutFields_SaysNothingWasFound()
    {
        var message = FormSummary.Describe(Array.Empty<FormField>(), Elapsed);

        Assert.Equal("Aucun champ de formulaire trouvé dans cette fenêtre. Lu en 120 ms.", message);
    }

    [Fact]
    public void Describe_WithOneField_UsesSingular()
    {
        var fields = new[] { Field("Nom", FieldKind.Text, hasValue: false) };

        var message = FormSummary.Describe(fields, Elapsed);

        Assert.Equal("1 champ trouvé : Nom (vide). Lu en 120 ms.", message);
    }

    [Fact]
    public void Describe_WithSeveralFields_DescribesEachState()
    {
        var fields = new[]
        {
            Field("Nom", FieldKind.Text, hasValue: false),
            Field("Email", FieldKind.Text, hasValue: true),
            Field("Mot de passe", FieldKind.Password, hasValue: false),
            Field(string.Empty, FieldKind.Choice, hasValue: true),
        };

        var message = FormSummary.Describe(fields, Elapsed);

        Assert.Equal(
            "4 champs trouvés : Nom (vide), Email (rempli), Mot de passe (mot de passe), sans nom (rempli). Lu en 120 ms.",
            message);
    }

    [Fact]
    public void Describe_WithManyFields_ListsOnlyTheFirstOnes()
    {
        var fields = Enumerable.Range(1, FormSummary.MaxListedFields + 2)
            .Select(index => Field($"Champ {index}", FieldKind.Text, hasValue: false))
            .ToArray();

        var message = FormSummary.Describe(fields, Elapsed);

        Assert.StartsWith("7 champs trouvés : Champ 1 (vide)", message, StringComparison.Ordinal);
        Assert.Contains("Champ 5 (vide) et 2 autre(s).", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Champ 6", message, StringComparison.Ordinal);
    }

    private static FormField Field(string label, FieldKind kind, bool hasValue)
    {
        return new FormField { Label = label, Kind = kind, HasValue = hasValue };
    }
}
