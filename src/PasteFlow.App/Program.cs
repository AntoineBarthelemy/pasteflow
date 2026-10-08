using System.Windows;
using PasteFlow.App.Forms;
using PasteFlow.App.Highlights;
using PasteFlow.App.Hotkeys;
using PasteFlow.App.Notifications;
using PasteFlow.App.Screen;
using PasteFlow.App.Tray;
using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Undo;

namespace PasteFlow.App;

/// <summary>
/// Entry point: composes the application and runs it in the background.
/// </summary>
internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\PasteFlow.SingleInstance";

    [STAThread]
    private static int Main()
    {
        // Only one instance may own the global shortcut
        using var singleInstance = new Mutex(initiallyOwned: false, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            return 0;
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var notifier = new PopupNotifier();

        using var trayIcon = new TrayIcon(application.Shutdown);
        using var hotkeyListener = new Win32HotkeyListener();
        using var undoHotkeyListener = new Win32HotkeyListener();
        var formAccess = new UiAutomationFormAccess();
        var fillMemory = new FillMemory();
        using var choiceListener1 = new Win32HotkeyListener();
        using var choiceListener2 = new Win32HotkeyListener();
        using var choiceListener3 = new Win32HotkeyListener();
        using var suggestions = new SuggestionBubbleController(
            new SuggestionPicker(formAccess, fillMemory),
            new IHotkeyListener[] { choiceListener1, choiceListener2, choiceListener3 });
        using var highlighter = new UiAutomationFieldHighlighter(formAccess, suggestions);
        var candidateSources = new ICandidateSource[] { new ScreenCandidateSource(new UiAutomationWindowTextReader()) };
        using var activation = new ShortcutActivationService(
            hotkeyListener,
            formAccess,
            formAccess,
            candidateSources,
            notifier,
            fillMemory,
            highlighter);
        using var undo = new UndoShortcutService(undoHotkeyListener, fillMemory, formAccess, formAccess, notifier);

        // Without its own shortcut, undoing is simply not announced: filling still works
        var undoHotkey = Hotkey.DefaultUndo;
        if (undo.Start(undoHotkey))
        {
            activation.UndoHotkey = undoHotkey;
        }

        var hotkey = Hotkey.Default;
        if (activation.Start(hotkey))
        {
            trayIcon.ShowStatus(activation.UndoHotkey is null ? $"{hotkey} actif" : $"{hotkey} actif, {undoHotkey} pour annuler");
            notifier.Notify($"PasteFlow est lancé. Appuie sur {hotkey} pour le tester.");
        }
        else
        {
            trayIcon.ShowStatus("raccourci indisponible");
            notifier.Notify($"Le raccourci {hotkey} est déjà utilisé par une autre application.");
        }

        // The official lists are loaded now, in the background, rather than at the first fill
        _ = Task.Run(() => ReferenceData.Shared);

        return application.Run();
    }
}
