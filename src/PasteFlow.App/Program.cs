using System.Windows;
using PasteFlow.App.Hotkeys;
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

        using var trayIcon = new TrayIcon(application.Shutdown);
        using var hotkeyListener = new Win32HotkeyListener();
        using var activation = new ShortcutActivationService(hotkeyListener, trayIcon);

        var hotkey = Hotkey.Default;
        if (!activation.Start(hotkey))
        {
            trayIcon.Notify($"Le raccourci {hotkey} est déjà utilisé par une autre application.");
        }

        return application.Run();
    }
}
