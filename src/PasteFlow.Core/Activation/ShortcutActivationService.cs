using System.Diagnostics;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Notifications;

namespace PasteFlow.Core.Activation;

/// <summary>
/// Connects the global shortcut to the actions of the application.
/// </summary>
/// <remarks>
/// For now, pressing the shortcut reads the form of the active window and describes it to the user.
/// Filling the fields will be plugged in here.
/// </remarks>
public sealed class ShortcutActivationService : IDisposable
{
    private readonly IHotkeyListener _listener;
    private readonly IFormReader _formReader;
    private readonly IUserNotifier _notifier;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortcutActivationService"/> class.
    /// </summary>
    /// <param name="listener">The listener of the global shortcut, owned by the caller.</param>
    /// <param name="formReader">The component that reads the form of the active window.</param>
    /// <param name="notifier">The component used to display messages to the user.</param>
    public ShortcutActivationService(IHotkeyListener listener, IFormReader formReader, IUserNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(formReader);
        ArgumentNullException.ThrowIfNull(notifier);

        _listener = listener;
        _formReader = formReader;
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

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        var stopwatch = Stopwatch.StartNew();
        var fields = await _formReader.ReadActiveFormAsync(CancellationToken.None);
        stopwatch.Stop();

        if (_isDisposed)
        {
            return;
        }

        _notifier.Notify(FormSummary.Describe(fields, stopwatch.Elapsed));
    }
}
