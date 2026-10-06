using PasteFlow.Core.Hotkeys;

namespace PasteFlow.Core.Tests.Hotkeys;

public sealed class HotkeyTests
{
    [Fact]
    public void Default_IsControlAltV()
    {
        Assert.Equal(KeyModifiers.Control | KeyModifiers.Alt, Hotkey.Default.Modifiers);
        Assert.Equal('V', Hotkey.Default.Key);
    }

    [Fact]
    public void DefaultUndo_IsControlAltZ()
    {
        Assert.Equal("Ctrl+Alt+Z", Hotkey.DefaultUndo.ToString());
        Assert.NotEqual(Hotkey.Default, Hotkey.DefaultUndo);
    }

    [Fact]
    public void Constructor_WithLowerCaseKey_NormalizesKeyToUpperCase()
    {
        var hotkey = new Hotkey(KeyModifiers.Control, 'k');

        Assert.Equal('K', hotkey.Key);
    }

    [Fact]
    public void Constructor_WithoutModifier_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Hotkey(KeyModifiers.None, 'V'));
    }

    [Theory]
    [InlineData('%')]
    [InlineData(' ')]
    [InlineData('é')]
    public void Constructor_WithUnsupportedKey_ThrowsArgumentException(char key)
    {
        Assert.Throws<ArgumentException>(() => new Hotkey(KeyModifiers.Control, key));
    }

    [Theory]
    [InlineData("Ctrl+Alt+V", KeyModifiers.Control | KeyModifiers.Alt, 'V')]
    [InlineData("ctrl + alt + v", KeyModifiers.Control | KeyModifiers.Alt, 'V')]
    [InlineData("Control+Shift+K", KeyModifiers.Control | KeyModifiers.Shift, 'K')]
    [InlineData("Win+1", KeyModifiers.Windows, '1')]
    public void TryParse_WithValidText_ReturnsHotkey(string text, KeyModifiers expectedModifiers, char expectedKey)
    {
        var isParsed = Hotkey.TryParse(text, out var hotkey);

        Assert.True(isParsed);
        Assert.NotNull(hotkey);
        Assert.Equal(expectedModifiers, hotkey.Modifiers);
        Assert.Equal(expectedKey, hotkey.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("V")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Ctrl+V")]
    [InlineData("Ctrl+Foo+V")]
    [InlineData("Ctrl+VV")]
    [InlineData("Ctrl+%")]
    public void TryParse_WithInvalidText_ReturnsFalse(string? text)
    {
        var isParsed = Hotkey.TryParse(text, out var hotkey);

        Assert.False(isParsed);
        Assert.Null(hotkey);
    }

    [Fact]
    public void Parse_WithInvalidText_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => Hotkey.Parse("not a shortcut"));
    }

    [Theory]
    [InlineData("alt+ctrl+v", "Ctrl+Alt+V")]
    [InlineData("shift + win + control + a", "Ctrl+Shift+Win+A")]
    [InlineData("Alt+7", "Alt+7")]
    public void ToString_ReturnsCanonicalText(string text, string expected)
    {
        var hotkey = Hotkey.Parse(text);

        Assert.Equal(expected, hotkey.ToString());
    }

    [Fact]
    public void Equals_WithSameModifiersAndKey_ReturnsTrue()
    {
        var hotkey = new Hotkey(KeyModifiers.Alt | KeyModifiers.Control, 'v');

        Assert.Equal(Hotkey.Default, hotkey);
    }
}
