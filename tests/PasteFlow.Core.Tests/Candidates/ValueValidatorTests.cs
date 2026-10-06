using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Tests.Candidates;

public sealed class ValueValidatorTests
{
    [Theory]
    [InlineData("FR76 3000 6000 0112 3456 7890 189")]
    [InlineData("FR7630006000011234567890189")]
    [InlineData("DE89 3704 0044 0532 0130 00")]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    public void IsValid_AcceptsIbanWithCorrectKey(string iban)
    {
        Assert.True(ValueValidator.IsValid(CandidateKind.Iban, iban));
    }

    [Theory]
    [InlineData("FR76 3000 6000 0112 3456 7890 188")]
    [InlineData("FR00 3000 6000 0112 3456 7890 189")]
    [InlineData("FR76 3000")]
    public void IsValid_RejectsIbanWithWrongKeyOrLength(string iban)
    {
        Assert.False(ValueValidator.IsValid(CandidateKind.Iban, iban));
    }

    [Theory]
    [InlineData("12/10/2026")]
    [InlineData("29/02/2024")]
    [InlineData("05.10.26")]
    [InlineData("2026-10-05")]
    [InlineData("10/25/2026")]
    public void IsValid_AcceptsExistingDate(string date)
    {
        Assert.True(ValueValidator.IsValid(CandidateKind.Date, date));
    }

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("29/02/2026")]
    [InlineData("00/10/2026")]
    [InlineData("32/01/2026")]
    [InlineData("2026-13-01")]
    public void IsValid_RejectsImpossibleDate(string date)
    {
        Assert.False(ValueValidator.IsValid(CandidateKind.Date, date));
    }

    [Theory]
    [InlineData("antoine.b@orange.fr", true)]
    [InlineData("a.b-c@mail.univ-reims.fr", true)]
    [InlineData(".antoine@orange.fr", false)]
    [InlineData("antoine.@orange.fr", false)]
    [InlineData("antoine..b@orange.fr", false)]
    [InlineData("antoine@-orange.fr", false)]
    [InlineData("antoine@orange..fr", false)]
    public void IsValid_ChecksEmailShape(string email, bool expected)
    {
        Assert.Equal(expected, ValueValidator.IsValid(CandidateKind.Email, email));
    }

    [Fact]
    public void IsValid_AcceptsKindsWithoutRule()
    {
        Assert.True(ValueValidator.IsValid(CandidateKind.Reference, "CMD-48213"));
        Assert.True(ValueValidator.IsValid(CandidateKind.Text, "Bonjour"));
    }

    [Theory]
    [InlineData("51500", true)]
    [InlineData("75010", true)]
    [InlineData("20000", true)]
    [InlineData("97400", true)]
    [InlineData("98000", true)]
    [InlineData("00123", false)]
    [InlineData("96000", false)]
    [InlineData("99000", false)]
    [InlineData("5150", false)]
    [InlineData("515000", false)]
    [InlineData("5150A", false)]
    public void IsFrenchPostalCode_ChecksLengthAndDepartment(string value, bool expected)
    {
        Assert.Equal(expected, ValueValidator.IsFrenchPostalCode(value));
    }
}
