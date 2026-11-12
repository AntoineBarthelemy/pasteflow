using System.Runtime.InteropServices;
using PasteFlow.App.Interop;
using PasteFlow.Core.Picking;

namespace PasteFlow.App.Picking;

/// <summary>
/// Writes text at the cursor of the active window by typing it, as the keyboard would.
/// </summary>
/// <remarks>
/// <para>Typing works in any application and leaves the clipboard untouched. Line breaks are typed as Maj+Entrée,
/// so that a chat box does not send the message halfway.</para>
/// <para>The shortcut keys may still be held when the text is ready: typing waits until they are released, since
/// Ctrl or Alt held down would turn the letters into shortcuts of the application.</para>
/// </remarks>
internal sealed class KeyboardTextInserter : ITextInserter
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x2;
    private const uint KeyEventUnicode = 0x4;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyReturn = 0x0D;
    private const ushort VirtualKeyInsert = 0x2D;
    private const uint KeyEventExtended = 0x1;

    private static readonly int[] ModifierKeys = { 0x10, 0x11, 0x12, 0x5B, 0x5C };
    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ReleasePollDelay = TimeSpan.FromMilliseconds(20);

    private readonly IClipboardWriter? _clipboard;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardTextInserter"/> class.
    /// </summary>
    /// <param name="clipboard">The clipboard a terminal is pasted from, or <see langword="null"/> to always type.</param>
    public KeyboardTextInserter(IClipboardWriter? clipboard = null)
    {
        _clipboard = clipboard;
    }

    /// <inheritdoc/>
    public async Task<bool> InsertAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return true;
        }

        if (!await WaitForModifiersReleasedAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        // A terminal is pasted into, as the user would with Shift+Insert: typed keys may be lost by it, and a line
        // break typed would run a command (seen on 12/11/2026 in Windows Terminal)
        if (_clipboard is not null && TerminalWindows.IsTerminal(ForegroundProgram()))
        {
            // Kept out of the history here: the caller copies it again as it should be kept
            if (!await _clipboard.CopyAsync(text, isSensitive: true, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            var paste = PasteKeystrokes();
            return NativeMethods.SendInput((uint)paste.Length, paste, Marshal.SizeOf<Input>()) == paste.Length;
        }

        var inputs = Keystrokes(text);
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        return sent == inputs.Length;
    }

    /// <summary>
    /// Builds the keystrokes of Shift+Insert, the paste every terminal knows.
    /// </summary>
    /// <returns>The keystrokes.</returns>
    internal static Input[] PasteKeystrokes()
    {
        return new[]
        {
            Key(VirtualKeyShift, isUp: false),
            Key(VirtualKeyInsert, isUp: false, KeyEventExtended),
            Key(VirtualKeyInsert, isUp: true, KeyEventExtended),
            Key(VirtualKeyShift, isUp: true),
        };
    }

    /// <summary>
    /// Types a text right away where the keyboard focus is, once the keys of the shortcut are released.
    /// </summary>
    /// <param name="text">The text to type.</param>
    /// <returns><see langword="true"/> if every keystroke was sent; otherwise <see langword="false"/>.</returns>
    internal static bool TypeNow(string text)
    {
        var deadline = DateTime.UtcNow + ReleaseTimeout;
        while (ModifierKeys.Any(key => NativeMethods.GetAsyncKeyState(key) < 0))
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            Thread.Sleep(ReleasePollDelay);
        }

        var inputs = Keystrokes(text);
        return NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }

    /// <summary>
    /// Turns a text into keystrokes: each character typed as itself, line breaks as Maj+Entrée.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The keystrokes, key down then key up for each.</returns>
    internal static Input[] Keystrokes(string text)
    {
        var inputs = new List<Input>(text.Length * 2);
        foreach (var character in text.Replace("\r\n", "\n", StringComparison.Ordinal))
        {
            switch (character)
            {
                case '\r':
                case '\n':
                    inputs.Add(Key(VirtualKeyShift, isUp: false));
                    inputs.Add(Key(VirtualKeyReturn, isUp: false));
                    inputs.Add(Key(VirtualKeyReturn, isUp: true));
                    inputs.Add(Key(VirtualKeyShift, isUp: true));
                    break;
                default:
                    // A tab would move to the next field: it is written as a space
                    var typed = character == '\t' ? ' ' : character;
                    inputs.Add(Character(typed, isUp: false));
                    inputs.Add(Character(typed, isUp: true));
                    break;
            }
        }

        return inputs.ToArray();
    }

    private static string ForegroundProgram()
    {
        _ = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out var processId);
        return processId == 0 ? string.Empty : PasteFlow.App.Screen.WindowProgram.NameOf((int)processId);
    }

    private static Input Key(ushort virtualKey, bool isUp, uint extraFlags = 0)
    {
        return new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = (isUp ? KeyEventKeyUp : 0) | extraFlags } },
        };
    }

    private static Input Character(char character, bool isUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput { Scan = character, Flags = KeyEventUnicode | (isUp ? KeyEventKeyUp : 0) },
            },
        };
    }

    private static async Task<bool> WaitForModifiersReleasedAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ReleaseTimeout;
        while (ModifierKeys.Any(key => NativeMethods.GetAsyncKeyState(key) < 0))
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }

            await Task.Delay(ReleasePollDelay, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
