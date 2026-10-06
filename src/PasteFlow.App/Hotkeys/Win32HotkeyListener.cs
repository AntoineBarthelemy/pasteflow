using System.Windows.Interop;
using PasteFlow.App.Interop;
using PasteFlow.Core.Hotkeys;

namespace PasteFlow.App.Hotkeys;

/// <summary>
/// Listens to a global shortcut through the Win32 <c>RegisterHotKey</c> function.
/// </summary>
/// <remarks>
/// An invisible message-only window receives the WM_HOTKEY messages. It must be created on the user interface thread.
/// </remarks>
internal sealed class Win32HotkeyListener : IHotkeyListener
{
    private const int HotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private static readonly IntPtr MessageOnlyWindowParent = new(-3);

    private readonly HwndSource _messageWindow;
    private bool _isRegistered;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Win32HotkeyListener"/> class.
    /// </summary>
    public Win32HotkeyListener()
    {
        var parameters = new HwndSourceParameters("PasteFlow.HotkeyListener")
        {
            ParentWindow = MessageOnlyWindowParent,
            WindowStyle = 0,
        };

        _messageWindow = new HwndSource(parameters);
        _messageWindow.AddHook(WndProc);
    }

    /// <inheritdoc/>
    public event EventHandler? HotkeyPressed;

    /// <inheritdoc/>
    public bool TryRegister(Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        Unregister();

        // MOD_NOREPEAT avoids a burst of notifications while the keys stay pressed
        var modifiers = (uint)hotkey.Modifiers | ModNoRepeat;
        _isRegistered = NativeMethods.RegisterHotKey(_messageWindow.Handle, HotkeyId, modifiers, hotkey.Key);
        return _isRegistered;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Unregister();
        _messageWindow.RemoveHook(WndProc);
        _messageWindow.Dispose();
        _isDisposed = true;
    }

    /// <inheritdoc/>
    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        _ = NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        _isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt64() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }
}
