namespace PasteFlow.App.Tray;

/// <summary>
/// An entry of the menu of PasteFlow opened with the keyboard.
/// </summary>
/// <param name="Letter">The letter that runs the entry.</param>
/// <param name="Text">What the entry does, such as "Ma fiche…".</param>
/// <param name="Run">The action run.</param>
/// <param name="IsEnabled">Whether the entry can be run now.</param>
/// <param name="Icon">The icon of the entry, a glyph of the icon font of Windows, or an empty string.</param>
internal sealed record MenuEntry(char Letter, string Text, Action Run, bool IsEnabled = true, string Icon = "");
