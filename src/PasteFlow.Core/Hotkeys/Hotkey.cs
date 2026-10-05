using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PasteFlow.Core.Hotkeys;

/// <summary>
/// A global keyboard shortcut made of modifier keys and a single letter or digit key.
/// </summary>
public sealed record Hotkey
{
    private const char Separator = '+';

    /// <summary>
    /// Initializes a new instance of the <see cref="Hotkey"/> class.
    /// </summary>
    /// <param name="modifiers">The modifier keys that must be held down.</param>
    /// <param name="key">The main key: a letter from A to Z or a digit from 0 to 9, case insensitive.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when no modifier key is given or when the main key is not supported.
    /// </exception>
    public Hotkey(KeyModifiers modifiers, char key)
    {
        if (modifiers == KeyModifiers.None)
        {
            throw new ArgumentException("A shortcut needs at least one modifier key.", nameof(modifiers));
        }

        var normalizedKey = char.ToUpperInvariant(key);
        if (!IsSupportedKey(normalizedKey))
        {
            throw new ArgumentException("The main key must be a letter or a digit.", nameof(key));
        }

        Modifiers = modifiers;
        Key = normalizedKey;
    }

    /// <summary>
    /// Gets the default shortcut of the application: Ctrl+Alt+V.
    /// </summary>
    public static Hotkey Default { get; } = new(KeyModifiers.Control | KeyModifiers.Alt, 'V');

    /// <summary>
    /// Gets the modifier keys that must be held down.
    /// </summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>
    /// Gets the main key, always an upper case letter or a digit.
    /// </summary>
    public char Key { get; }

    /// <summary>
    /// Converts a text such as "Ctrl+Alt+V" into a shortcut.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The shortcut described by the text.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a valid shortcut.</exception>
    public static Hotkey Parse(string text)
    {
        return TryParse(text, out var hotkey)
            ? hotkey
            : throw new FormatException($"'{text}' is not a valid keyboard shortcut.");
    }

    /// <summary>
    /// Tries to convert a text such as "Ctrl+Alt+V" into a shortcut.
    /// </summary>
    /// <param name="text">The text to convert, case insensitive, spaces around "+" are allowed.</param>
    /// <param name="hotkey">The shortcut described by the text, or <see langword="null"/> if the text is invalid.</param>
    /// <returns><see langword="true"/> if the text is a valid shortcut; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out Hotkey? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split(Separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var modifiers = KeyModifiers.None;
        foreach (var part in parts[..^1])
        {
            if (!TryParseModifier(part, out var modifier) || modifiers.HasFlag(modifier))
            {
                return false;
            }

            modifiers |= modifier;
        }

        var keyPart = parts[^1];
        if (keyPart.Length != 1 || !IsSupportedKey(char.ToUpperInvariant(keyPart[0])))
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, keyPart[0]);
        return true;
    }

    /// <summary>
    /// Returns the canonical text of the shortcut, for example "Ctrl+Alt+V".
    /// </summary>
    /// <returns>The modifiers in the order Ctrl, Alt, Shift, Win, followed by the main key.</returns>
    public override string ToString()
    {
        var parts = new List<string>(capacity: 5);
        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(KeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key.ToString(CultureInfo.InvariantCulture));
        return string.Join(Separator, parts);
    }

    private static bool IsSupportedKey(char key)
    {
        return char.IsAsciiLetterUpper(key) || char.IsAsciiDigit(key);
    }

    private static bool TryParseModifier(string text, out KeyModifiers modifier)
    {
        modifier = text.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => KeyModifiers.Control,
            "ALT" => KeyModifiers.Alt,
            "SHIFT" => KeyModifiers.Shift,
            "WIN" or "WINDOWS" => KeyModifiers.Windows,
            _ => KeyModifiers.None,
        };

        return modifier != KeyModifiers.None;
    }
}
