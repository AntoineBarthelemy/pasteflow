using System.Windows;
using PasteFlow.App.Forms;
using PasteFlow.App.Hotkeys;
using PasteFlow.App.Notifications;
using PasteFlow.App.Tray;
using PasteFlow.Core.Activation;
using PasteFlow.Core.Hotkeys;

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
        using var activation = new ShortcutActivationService(hotkeyListener, new UiAutomationFormReader(), notifier);

        var hotkey = Hotkey.Default;
        if (activation.Start(hotkey))
        {
            trayIcon.ShowStatus($"{hotkey} actif");
            notifier.Notify($"PasteFlow est lancé. Appuie sur {hotkey} pour le tester.");
        }
        else
        {
            trayIcon.ShowStatus("raccourci indisponible");
            notifier.Notify($"Le raccourci {hotkey} est déjà utilisé par une autre application.");
        }

        return application.Run();
    }
}
