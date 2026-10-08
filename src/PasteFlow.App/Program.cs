using System.Windows;
using PasteFlow.App.Forms;
using PasteFlow.App.Highlights;
using PasteFlow.App.History;
using PasteFlow.App.Hotkeys;
using PasteFlow.App.Notifications;
using PasteFlow.App.Screen;
using PasteFlow.App.Settings;
using PasteFlow.App.Tray;
using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.History;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Settings;
using PasteFlow.Core.Undo;

namespace PasteFlow.App;

/// <summary>
/// Entry point: composes the application and runs it in the background.
/// </summary>
internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\PasteFlow.SingleInstance";
    private const string StartupEntryName = "PasteFlow";

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
        var settingsStore = new JsonSettingsStore(JsonSettingsStore.DefaultPath);
        var startup = new StartupRegistration(StartupEntryName);
        var settings = settingsStore.Load() with { StartWithWindows = startup.IsEnabled };

        // What the fills learn and what can be undone
        using var history = new FillHistory(new ProtectedFileHistoryStore(ProtectedFileHistoryStore.DefaultPath));
        var fillMemory = new FillMemory();
        history.Observe(fillMemory);

        // Reading and writing forms, frames and suggestions
        var formAccess = new UiAutomationFormAccess();
        using var choiceListener1 = new Win32HotkeyListener();
        using var choiceListener2 = new Win32HotkeyListener();
        using var choiceListener3 = new Win32HotkeyListener();
        using var suggestions = new SuggestionBubbleController(
            new SuggestionPicker(formAccess, fillMemory),
            new IHotkeyListener[] { choiceListener1, choiceListener2, choiceListener3 });
        using var highlighter = new UiAutomationFieldHighlighter(formAccess, suggestions, history);

        // The shortcuts
        using var hotkeyListener = new Win32HotkeyListener();
        using var undoHotkeyListener = new Win32HotkeyListener();
        var candidateSources = new ICandidateSource[] { new ScreenCandidateSource(new UiAutomationWindowTextReader()), history };
        using var activation = new ShortcutActivationService(
            hotkeyListener,
            formAccess,
            formAccess,
            candidateSources,
            notifier,
            fillMemory,
            highlighter);
        using var undo = new UndoShortcutService(undoHotkeyListener, fillMemory, formAccess, formAccess, notifier);

        void Apply(AppSettings chosen)
        {
            activation.Options = MatchingOptions.For(chosen.Prudence);
            history.IsEnabled = chosen.LearnFromFills;
        }

        SettingsWindow? settingsWindow = null;
        void OpenSettings()
        {
            if (settingsWindow is { IsVisible: true })
            {
                _ = settingsWindow.Activate();
                return;
            }

            settingsWindow = new SettingsWindow(settings, chosen =>
            {
                settings = chosen;
                Apply(chosen);
                var isSaved = settingsStore.TrySave(chosen);
                var isStartupSet = Environment.ProcessPath is { } path && startup.TrySet(chosen.StartWithWindows, path);
                notifier.Notify(isSaved && isStartupSet
                    ? "Paramètres enregistrés."
                    : "Paramètres appliqués, mais ils n'ont pas pu être tous enregistrés.");
            });
            settingsWindow.Show();
        }

        Apply(settings);
        using var trayIcon = new TrayIcon(application.Shutdown, () => _ = ClearHistoryAsync(history, notifier), OpenSettings);

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

    private static async Task ClearHistoryAsync(FillHistory history, PopupNotifier notifier)
    {
        await history.ClearAsync(CancellationToken.None);
        notifier.Notify("Historique effacé : PasteFlow a oublié les valeurs apprises.");
    }
}
