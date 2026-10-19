using System.Windows;
using PasteFlow.App.Interop;
using PasteFlow.App.Notifications;
using PasteFlow.App.Screen;
using PasteFlow.Core.Hints;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Picking;

namespace PasteFlow.App.Hints;

/// <summary>
/// Pastes a value seen on screen without the mouse: its shortcut puts a code of letters on each value, typing a code
/// pastes its value where the cursor was.
/// </summary>
internal sealed class HintService : IDisposable
{
    private static readonly TimeSpan FocusDelay = TimeSpan.FromMilliseconds(80);

    private readonly IHotkeyListener _listener;
    private readonly ITextInserter _inserter;
    private readonly PopupNotifier _notifier;
    private bool _isBusy;
    private bool _isStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="HintService"/> class.
    /// </summary>
    /// <param name="listener">The listener of the shortcut, created on the user interface thread.</param>
    /// <param name="inserter">The component that types the value chosen.</param>
    /// <param name="notifier">The notifier that tells when there is nothing to choose.</param>
    public HintService(IHotkeyListener listener, ITextInserter inserter, PopupNotifier notifier)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(inserter);
        ArgumentNullException.ThrowIfNull(notifier);

        _listener = listener;
        _inserter = inserter;
        _notifier = notifier;
    }

    /// <summary>
    /// Starts reacting to the shortcut.
    /// </summary>
    /// <param name="hotkey">The shortcut, such as Ctrl+Alt+F.</param>
    /// <returns><see langword="true"/> if the shortcut is free; otherwise <see langword="false"/>.</returns>
    public bool Start(Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);

        if (_isStarted || !_listener.TryRegister(hotkey))
        {
            return false;
        }

        _isStarted = true;
        _listener.HotkeyPressed += OnHotkeyPressed;
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _listener.HotkeyPressed -= OnHotkeyPressed;
    }

    private static Rect WholeScreen()
    {
        var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
        return new Rect(screen.Left, screen.Top, screen.Width, screen.Height);
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        // A second press while the letters are being put is ignored
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await ChooseAndPasteAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _notifier.Notify("Impossible d'afficher les lettres sur l'écran.");
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ChooseAndPasteAsync()
    {
        // The value goes back where the cursor was: the window in front before the letters
        var origin = NativeMethods.GetForegroundWindow();
        var texts = await VisibleTextReader.ReadAsync(CancellationToken.None);
        var targets = await Task.Run(() => HintTargets.From(texts));
        if (targets.Count == 0)
        {
            _notifier.Notify("Aucune valeur lisible à l'écran.");
            return;
        }

        var overlay = new HintOverlayWindow(targets, WholeScreen());
        overlay.Show();
        if (await overlay.Choice is not { } chosen)
        {
            return;
        }

        _ = NativeMethods.SetForegroundWindow(origin);
        await Task.Delay(FocusDelay);
        if (!await _inserter.InsertAsync(chosen.Value, CancellationToken.None))
        {
            _notifier.Notify("La valeur n'a pas pu être tapée : relâche les touches puis réessaie.");
        }
    }
}
