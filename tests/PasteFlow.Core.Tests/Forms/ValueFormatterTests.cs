using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Forms;

public sealed class ValueFormatterTests
{
    [Theory]
    [InlineData("12/03/1990", "Date de naissance (AAAA-MM-JJ)", "", "1990-03-12")]
    [InlineData("1990-03-12", "Date de naissance", "JJ/MM/AAAA", "12/03/1990")]
    [InlineData("12.03.90", "Date", "DD/MM/YYYY", "12.03.90")]
    [InlineData("15/06/45", "Date de naissance", "JJ/MM/AAAA", "15/06/45")]
    [InlineData("15/06/45", "Date", "JJ-MM-AA", "15-06-45")]
    [InlineData("12/03/1990", "Date", "MM/DD/YYYY", "03/12/1990")]
    [InlineData("12/03/1990", "Date", "JJMMAAAA", "12031990")]
    [InlineData("12/03/1990", "Date", "ex : 31-12-2000", "12-03-1990")]
    [InlineData("12/03/1990", "Date", "ex : 2000-12-31", "1990-03-12")]
    [InlineData("12/03/1990", "Date de naissance", "", "12/03/1990")]
    public void Adapt_WritesDatesInTheFormatOfTheField(string value, string label, string hint, string expected)
    {
        Assert.Equal(expected, ValueFormatter.Adapt(value, CandidateKind.Date, Field(label, hint)));
    }

    [Theory]
    [InlineData("06 47 21 85 39", "ex : 0612345678", "0647218539")]
    [InlineData("0647218539", "ex : 06 12 34 56 78", "06 47 21 85 39")]
    [InlineData("06.47.21.85.39", "+33 6 12 34 56 78", "+33 6 47 21 85 39")]
    [InlineData("06 47 21 85 39", "+33612345678", "+33647218539")]
    [InlineData("+33 (0)6 47 21 85 39", "ex : 0612345678", "0647218539")]
    [InlineData("06 47 21 85 39", "10 chiffres sans espaces", "0647218539")]
    [InlineData("06 47 21 85 39", "", "06 47 21 85 39")]
    [InlineData("+44 20 7946 0958", "ex : 0612345678", "+44 20 7946 0958")]
    public void Adapt_WritesFrenchPhoneNumbersInTheFormatOfTheField(string value, string hint, string expected)
    {
        Assert.Equal(expected, ValueFormatter.Adapt(value, CandidateKind.Phone, Field("Téléphone", hint)));
    }

    [Theory]
    [InlineData("FR76 3000 6000 0112 3456 7890 189", "FR7630006000011234567890189", "FR7630006000011234567890189")]
    [InlineData("FR7630006000011234567890189", "FR76 3000 6000 0112 3456 7890 189", "FR76 3000 6000 0112 3456 7890 189")]
    [InlineData("FR76 3000 6000 0112 3456 7890 189", "", "FR76 3000 6000 0112 3456 7890 189")]
    public void Adapt_WritesIbansWithOrWithoutSpacesAsTheFieldShows(string value, string hint, string expected)
    {
        Assert.Equal(expected, ValueFormatter.Adapt(value, CandidateKind.Iban, Field("IBAN", hint)));
    }

    [Theory]
    [InlineData("1 250,00 €", "0,00", "1250,00")]
    [InlineData("1 250,00 €", "ex : 99.90", "1250.00")]
    [InlineData("$19.99", "0,00", "19,99")]
    [InlineData("1250.50 EUR", "0.00", "1250.50")]
    [InlineData("49,90 €", "", "49,90 €")]
    [InlineData("$1,234.56", "0.00", "1234.56")]
    [InlineData("€1,234", "0,00", "1234")]
    [InlineData("1.250,00 €", "0.00", "1250.00")]
    [InlineData("1250.5 EUR", "0,00", "1250,50")]
    public void Adapt_WritesBareNumbersWhenTheFieldShowsOne(string value, string hint, string expected)
    {
        Assert.Equal(expected, ValueFormatter.Adapt(value, CandidateKind.Amount, Field("Montant", hint)));
    }

    [Theory]
    [InlineData(CandidateKind.Email, "Antoine.B@orange.fr")]
    [InlineData(CandidateKind.Text, "12/03/1990")]
    [InlineData(CandidateKind.Reference, "CMD-48213")]
    public void Adapt_LeavesOtherKindsUnchanged(CandidateKind kind, string value)
    {
        Assert.Equal(value, ValueFormatter.Adapt(value, kind, Field("Champ", "JJ/MM/AAAA 0612345678 0,00")));
    }

    [Theory]
    [InlineData("N° de sécurité sociale (13 chiffres)", "2850551454123")]
    [InlineData("N° de sécurité sociale", "285055145412395")]
    public void Adapt_SocialSecurityNumber_LeavesItsKeyOutWhenThirteenDigitsAreAsked(string label, string expected)
    {
        Assert.Equal(expected, ValueFormatter.Adapt("285055145412395", CandidateKind.Number, Field(label, string.Empty)));
    }

    [Theory]
    [InlineData("Surface habitable (m²)", "64")]
    [InlineData("Surface habitable", "64 m²")]
    public void Adapt_Measure_LeavesItsUnitOutWhenTheFieldShowsIt(string label, string expected)
    {
        Assert.Equal(expected, ValueFormatter.Adapt("64 m²", CandidateKind.Measure, Field(label, string.Empty)));
    }

    [Fact]
    public void Adapt_LeavesAValueItCannotReadUnchanged()
    {
        Assert.Equal("31/02/2026", ValueFormatter.Adapt("31/02/2026", CandidateKind.Date, Field("Date", "AAAA-MM-JJ")));
    }

    private static FormField Field(string label, string hint)
    {
        return TestFields.Text("field", label) with { Hint = hint };
    }
}
