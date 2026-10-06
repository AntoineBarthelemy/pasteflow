using PasteFlow.Core.Hotkeys;

namespace PasteFlow.App.Tests.Fakes;

/// <summary>
/// In-memory listener that lets tests simulate a key press.
/// </summary>
internal sealed class FakeHotkeyListener : IHotkeyListener
{
    public event EventHandler? HotkeyPressed;

    public bool AcceptsRegistration { get; init; } = true;

    public Hotkey? RegisteredHotkey { get; private set; }

    public bool TryRegister(Hotkey hotkey)
    {
        if (!AcceptsRegistration)
        {
            return false;
        }

        RegisteredHotkey = hotkey;
        return true;
    }

    public void Unregister()
    {
        RegisteredHotkey = null;
    }

    public void SimulatePress()
    {
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        RegisteredHotkey = null;
    }
}
