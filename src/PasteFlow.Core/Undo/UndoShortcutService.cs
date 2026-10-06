using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Notifications;

namespace PasteFlow.Core.Undo;

/// <summary>
/// Connects the undo shortcut to the emptying of the fields written by the last fill.
/// </summary>
/// <remarks>
/// Only the fields the application wrote are emptied, and only if they still hold the value written:
/// a field the user changed since the fill is left as it is, so that the user's own work is never erased.
/// </remarks>
public sealed class UndoShortcutService : IDisposable
{
    private readonly IHotkeyListener _listener;
    private readonly FillMemory _fillMemory;
    private readonly IFieldValueReader _valueReader;
    private readonly IFieldWriter _fieldWriter;
    private readonly IUserNotifier _notifier;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UndoShortcutService"/> class.
    /// </summary>
    /// <param name="listener">The listener of the undo shortcut, owned by the caller.</param>
    /// <param name="fillMemory">The memory of the last fill, shared with the fill service.</param>
    /// <param name="valueReader">The component that reads the current value of the fields.</param>
    /// <param name="fieldWriter">The component that empties the fields.</param>
    /// <param name="notifier">The component used to display messages to the user.</param>
    public UndoShortcutService(
        IHotkeyListener listener,
        FillMemory fillMemory,
        IFieldValueReader valueReader,
        IFieldWriter fieldWriter,
        IUserNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(fillMemory);
        ArgumentNullException.ThrowIfNull(valueReader);
        ArgumentNullException.ThrowIfNull(fieldWriter);
        ArgumentNullException.ThrowIfNull(notifier);

        _listener = listener;
        _fillMemory = fillMemory;
        _valueReader = valueReader;
        _fieldWriter = fieldWriter;
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
        var report = await UndoLastFillAsync(CancellationToken.None);
        if (_isDisposed)
        {
            return;
        }

        _notifier.Notify(report.Describe());
    }

    private async Task<UndoReport> UndoLastFillAsync(CancellationToken cancellationToken)
    {
        var emptiedCount = 0;
        var changedCount = 0;
        var failedCount = 0;
        foreach (var written in _fillMemory.TakeLastFill())
        {
            var currentValue = await _valueReader.TryReadValueAsync(written.Field, cancellationToken);
            if (currentValue is null)
            {
                failedCount++;
            }
            else if (!UndoRules.IsStillWritten(written.Value, currentValue))
            {
                changedCount++;
            }
            else if (await _fieldWriter.TryWriteAsync(written.Field, string.Empty, cancellationToken))
            {
                emptiedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        return new UndoReport { EmptiedCount = emptiedCount, ChangedCount = changedCount, FailedCount = failedCount };
    }
}
