using System.Runtime.InteropServices;
using PasteFlow.App.Interop;
using PasteFlow.App.Picking;

namespace PasteFlow.App.Tests.Picking;

public sealed class KeyboardTextInserterTests
{
    private const uint KeyUp = 0x2;
    private const uint Unicode = 0x4;

    [Fact]
    public void Keystrokes_TypesEachCharacterDownThenUp()
    {
        var inputs = KeyboardTextInserter.Keystrokes("Léa");

        Assert.Equal(6, inputs.Length);
        Assert.Equal(new ushort[] { 'L', 'L', 'é', 'é', 'a', 'a' }, inputs.Select(input => input.Union.Keyboard.Scan));
        Assert.Equal(new[] { Unicode, Unicode | KeyUp }, inputs.Take(2).Select(input => input.Union.Keyboard.Flags));
    }

    [Fact]
    public void Keystrokes_TypesLineBreaksAsShiftEnter()
    {
        var inputs = KeyboardTextInserter.Keystrokes("a\r\nb");

        Assert.Equal(8, inputs.Length);
        Assert.Equal(new ushort[] { 0x10, 0x0D, 0x0D, 0x10 }, inputs.Skip(2).Take(4).Select(input => input.Union.Keyboard.VirtualKey));
    }

    [Fact]
    public void Keystrokes_TypesTabsAsSpaces()
    {
        var inputs = KeyboardTextInserter.Keystrokes("\t");

        Assert.All(inputs, input => Assert.Equal(' ', input.Union.Keyboard.Scan));
    }

    [Fact]
    public void Input_HasTheSizeWindowsExpects()
    {
        Assert.Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf<Input>());
    }
}
