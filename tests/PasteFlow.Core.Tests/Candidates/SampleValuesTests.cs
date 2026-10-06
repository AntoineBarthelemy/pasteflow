using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Tests.Candidates;

public sealed class SampleValuesTests
{
    [Theory]
    [InlineData("jean.dupont@exemple.fr", CandidateKind.Email, "")]
    [InlineData("contact@example.com", CandidateKind.Email, "")]
    [InlineData("prenom.nom@gmail.com", CandidateKind.Email, "")]
    [InlineData("John.Doe@outlook.com", CandidateKind.Email, "")]
    [InlineData("votre-email@orange.fr", CandidateKind.Email, "")]
    [InlineData("06 00 00 00 00", CandidateKind.Phone, "")]
    [InlineData("+33 6 00 00 00 00", CandidateKind.Phone, "")]
    [InlineData("06 12 34 56 78", CandidateKind.Phone, "")]
    [InlineData("01 23 45 67 89", CandidateKind.Phone, "")]
    [InlineData("Jean Dupont", CandidateKind.Text, "")]
    [InlineData("JJ/MM/AAAA", CandidateKind.Text, "")]
    [InlineData("XXXX", CandidateKind.Text, "")]
    [InlineData("00000", CandidateKind.Number, "")]
    [InlineData("Votre nom", CandidateKind.Text, "")]
    [InlineData("Lorem ipsum dolor", CandidateKind.Text, "")]
    public void IsSample_RecognizesExampleValue(string value, CandidateKind kind, string label)
    {
        Assert.True(SampleValues.IsSample(Candidate(value, kind, label)));
    }

    [Theory]
    [InlineData("antoine.b@orange.fr", CandidateKind.Email, "ex")]
    [InlineData("06 47 21 85 39", CandidateKind.Phone, "Exemple")]
    [InlineData("12/03/2005", CandidateKind.Date, "Format")]
    [InlineData("CMD-48213", CandidateKind.Reference, "e.g.")]
    public void IsSample_RecognizesValueIntroducedAsExample(string value, CandidateKind kind, string label)
    {
        Assert.True(SampleValues.IsSample(Candidate(value, kind, label)));
    }

    [Theory]
    [InlineData("antoine.b@orange.fr", CandidateKind.Email, "Email")]
    [InlineData("06 47 21 85 39", CandidateKind.Phone, "Tél.")]
    [InlineData("+33 6 47 21 85 39", CandidateKind.Phone, "")]
    [InlineData("CMD-48213", CandidateKind.Reference, "Commande n°")]
    [InlineData("Antoine Barthélémy", CandidateKind.Text, "Nom")]
    [InlineData("12/03/2005", CandidateKind.Date, "Date de naissance")]
    [InlineData("51500", CandidateKind.Number, "")]
    [InlineData("Index des commandes", CandidateKind.Text, "")]
    [InlineData("A", CandidateKind.Text, "")]
    [InlineData("00", CandidateKind.Text, "")]
    public void IsSample_KeepsRealValue(string value, CandidateKind kind, string label)
    {
        Assert.False(SampleValues.IsSample(Candidate(value, kind, label)));
    }

    [Fact]
    public void AppearsInHints_FindsValueInsidePlaceholder()
    {
        var hints = new[] { "ex : marie.martin@laposte.net" };

        Assert.True(SampleValues.AppearsInHints("marie.martin@laposte.net", hints));
        Assert.True(SampleValues.AppearsInHints("  MARIE.MARTIN@LAPOSTE.NET ", hints));
    }

    [Fact]
    public void AppearsInHints_IgnoresValuesAbsentFromHints()
    {
        Assert.False(SampleValues.AppearsInHints("antoine.b@orange.fr", new[] { "ex : marie.martin@laposte.net" }));
        Assert.False(SampleValues.AppearsInHints(string.Empty, new[] { "ex : marie.martin@laposte.net" }));
        Assert.False(SampleValues.AppearsInHints("antoine.b@orange.fr", Array.Empty<string>()));
    }

    private static Candidate Candidate(string value, CandidateKind kind, string label)
    {
        return new Candidate { Value = value, Kind = kind, Label = label, SourceName = "Formulaire", SourceRank = 0 };
    }
}
