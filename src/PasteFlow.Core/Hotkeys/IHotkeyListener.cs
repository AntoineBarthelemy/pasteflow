namespace PasteFlow.Core.Hotkeys;

/// <summary>
/// Listens to a global keyboard shortcut, whatever the application in the foreground.
/// </summary>
public interface IHotkeyListener : IDisposable
{
    /// <summary>
    /// Occurs when the registered shortcut is pressed.
    /// </summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Starts listening to the given shortcut, replacing the previous one if any.
    /// </summary>
    /// <param name="hotkey">The shortcut to listen to.</param>
    /// <returns>
    /// <see langword="true"/> if the shortcut is registered; <see langword="false"/> if it is already used by another application.
    /// </returns>
    bool TryRegister(Hotkey hotkey);
}
