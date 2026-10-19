using PasteFlow.Core.Hints;

namespace PasteFlow.Core.Tests.Hints;

public sealed class HintCodesTests
{
    [Fact]
    public void Create_UsesOneLetterForAFewValues()
    {
        Assert.Equal(new[] { "F", "J", "D" }, HintCodes.Create(3));
    }

    [Fact]
    public void Create_UsesTwoLettersBeyondEightValues()
    {
        var codes = HintCodes.Create(9);

        Assert.All(codes, code => Assert.Equal(2, code.Length));
        Assert.Equal("FF", codes[0]);
        Assert.Equal("FJ", codes[1]);
        Assert.Equal("JF", codes[8]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(64)]
    [InlineData(65)]
    public void Create_GivesDistinctCodesNoneStartingAnother(int count)
    {
        var codes = HintCodes.Create(count);

        Assert.Equal(count, codes.Count);
        Assert.Equal(count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(codes, code => codes.Any(other => other != code && other.StartsWith(code, StringComparison.Ordinal)));
    }

    [Fact]
    public void Create_OnlyUsesLettersAtTheSamePlaceInAzertyAndQwerty()
    {
        Assert.All(string.Concat(HintCodes.Create(64)), letter => Assert.Contains(letter, "SDFGHJKL"));
    }

    [Fact]
    public void Read_FindsTheCodeTypedWholeInAnyCase()
    {
        var codes = HintCodes.Create(9);

        Assert.Equal(((int?)1, true), HintCodes.Read(codes, "fj"));
    }

    [Fact]
    public void Read_WaitsForTheNextLetterOfACodeStarted()
    {
        Assert.Equal(((int?)null, true), HintCodes.Read(HintCodes.Create(9), "F"));
    }

    [Fact]
    public void Read_TellsWhenNoCodeStartsLikeThat()
    {
        Assert.Equal(((int?)null, false), HintCodes.Read(HintCodes.Create(9), "Z"));
    }
}
