using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Notifications;

namespace PasteFlow.Core.Activation;

/// <summary>
/// Connects the global shortcut to the actions of the application.
/// </summary>
/// <remarks>
/// For now, pressing the shortcut only notifies the user. Reading and filling forms will be plugged in here.
/// </remarks>
public sealed class ShortcutActivationService : IDisposable
{
    private readonly IHotkeyListener _listener;
    private readonly IUserNotifier _notifier;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortcutActivationService"/> class.
    /// </summary>
    /// <param name="listener">The listener of the global shortcut, owned by the caller.</param>
    /// <param name="notifier">The component used to display messages to the user.</param>
    public ShortcutActivationService(IHotkeyListener listener, IUserNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(notifier);

        _listener = listener;
        _notifier = notifier;
    }

    /// <summary>
    /// Gets the shortcut currently listened to, or <see langword="null"/> if the service is not started.
    /// </summary>
    public Hotkey? ActiveHotkey { get; private set; }

    /// <summary>
    /// Starts listening to the given shortcut.
    /// </summary>
    /// <param name="hotkey">The shortcut to listen to.</param>
    /// <returns>
    /// <see langword="true"/> if the service is started; <see langword="false"/> if the shortcut is already used by another application.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is already started.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the service is disposed.</exception>
    public bool Start(Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (ActiveHotkey is not null)
        {
            throw new InvalidOperationException("The service is already started.");
        }

        if (!_listener.TryRegister(hotkey))
        {
            return false;
        }

        _listener.HotkeyPressed += OnHotkeyPressed;
        ActiveHotkey = hotkey;
        return true;
    }

    /// <summary>
    /// Stops reacting to the shortcut. The listener itself is left to its owner.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _listener.HotkeyPressed -= OnHotkeyPressed;
        ActiveHotkey = null;
        _isDisposed = true;
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _notifier.Notify($"Raccourci {ActiveHotkey} détecté.");
    }
}
