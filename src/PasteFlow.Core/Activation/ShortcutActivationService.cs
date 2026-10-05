using System.Diagnostics;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Notifications;

namespace PasteFlow.Core.Activation;

/// <summary>
/// Connects the global shortcut to the actions of the application.
/// </summary>
/// <remarks>
/// Pressing the shortcut reads the form of the active window and fills the fields allowed by <see cref="FillRules"/>.
/// For now each field receives its own label, until the values are matched with the screen content.
/// </remarks>
public sealed class ShortcutActivationService : IDisposable
{
    private readonly IHotkeyListener _listener;
    private readonly IFormReader _formReader;
    private readonly IFieldWriter _fieldWriter;
    private readonly IUserNotifier _notifier;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortcutActivationService"/> class.
    /// </summary>
    /// <param name="listener">The listener of the global shortcut, owned by the caller.</param>
    /// <param name="formReader">The component that reads the form of the active window.</param>
    /// <param name="fieldWriter">The component that writes values into the fields read.</param>
    /// <param name="notifier">The component used to display messages to the user.</param>
    public ShortcutActivationService(
        IHotkeyListener listener,
        IFormReader formReader,
        IFieldWriter fieldWriter,
        IUserNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(formReader);
        ArgumentNullException.ThrowIfNull(fieldWriter);
        ArgumentNullException.ThrowIfNull(notifier);

        _listener = listener;
        _formReader = formReader;
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

    private static string DemoValueFor(FormField field)
    {
        return string.IsNullOrWhiteSpace(field.Label) ? "PasteFlow" : field.Label;
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        var report = await FillActiveFormAsync(CancellationToken.None);
        if (_isDisposed)
        {
            return;
        }

        _notifier.Notify(report.Describe());
    }

    private async Task<FillReport> FillActiveFormAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var fields = await _formReader.ReadActiveFormAsync(cancellationToken);

        var filledCount = 0;
        var failedCount = 0;
        foreach (var field in fields.Where(FillRules.CanFill))
        {
            if (await _fieldWriter.TryWriteAsync(field, DemoValueFor(field), cancellationToken))
            {
                filledCount++;
            }
            else
            {
                failedCount++;
            }
        }

        stopwatch.Stop();
        return new FillReport
        {
            FilledCount = filledCount,
            AlreadyFilledCount = fields.Count(field => field.Kind == FieldKind.Text && field.HasValue),
            FailedCount = failedCount,
            Elapsed = stopwatch.Elapsed,
        };
    }
}
