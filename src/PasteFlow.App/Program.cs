using System.IO;
using System.Windows;
using PasteFlow.App.Forms;
using PasteFlow.App.Highlights;
using PasteFlow.App.Hints;
using PasteFlow.App.History;
using PasteFlow.App.Hotkeys;
using PasteFlow.App.Logins;
using PasteFlow.App.Notifications;
using PasteFlow.App.Picking;
using PasteFlow.App.Profile;
using PasteFlow.App.Screen;
using PasteFlow.App.Security;
using PasteFlow.App.Settings;
using PasteFlow.App.Tray;
using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.History;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Security;
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

        // What the user keeps about themselves, encrypted on this computer
        using var profile = new ProfileBook(new ProtectedFileProfileStore(ProtectedFileProfileStore.DefaultPath));

        // Sensitive values open with Windows Hello for 30 minutes, and close as soon as the computer is locked
        using var sensitiveGate = new SensitiveGate(new WindowsHelloPresenceCheck(application.Dispatcher));
        using var sessionLockWatcher = new SessionLockWatcher(sensitiveGate);

        // The profile learns from what the user fills about themselves, and starts with the Windows account
        var learningPrompts = new PopupLearningPrompts(notifier, application.Dispatcher);
        using var learner = new ProfileLearner(
            profile,
            new ProtectedFileProfileStore(ProtectedFileProfileStore.LearningPath),
            sensitiveGate,
            learningPrompts);
        learner.Observe(history, fillMemory);

        // Sign-in forms are filled from Bitwarden, once its vault is unlocked with the master password
        using var bitwarden = new BitwardenCli();

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
        using var menuHotkeyListener = new Win32HotkeyListener();
        using var labelsHotkeyListener = new Win32HotkeyListener();
        var windowReader = new UiAutomationWindowTextReader(OcrTextRecognizer.TryCreate());
        var candidateSources = new ICandidateSource[] { new ScreenCandidateSource(windowReader), new ProfileCandidateSource(profile), history };
        using var activation = new ShortcutActivationService(
            hotkeyListener,
            formAccess,
            formAccess,
            candidateSources,
            notifier,
            fillMemory,
            highlighter);
        using var undo = new UndoShortcutService(undoHotkeyListener, fillMemory, formAccess, formAccess, notifier);

        // Outside a form, the shortcut pastes at the cursor, or lets the user choose
        activation.SensitiveGate = sensitiveGate;
        activation.PasswordManager = bitwarden;
        activation.LoginPicker = new WindowLoginPicker(application.Dispatcher);
        activation.CaretReader = formAccess;
        activation.Picker = new PastePicker();
        activation.Inserter = new KeyboardTextInserter();

        void Apply(AppSettings chosen)
        {
            activation.Options = MatchingOptions.For(chosen.Prudence);
            history.IsEnabled = chosen.LearnFromFills;
            learner.IsEnabled = chosen.LearnFromFills;
            windowReader.ReadImages = chosen.ReadImages;
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
            _ = settingsWindow.Activate();
        }

        ProfileWindow? profileWindow = null;
        void OpenProfile()
        {
            if (profileWindow is { IsVisible: true })
            {
                _ = profileWindow.Activate();
                return;
            }

            profileWindow = new ProfileWindow(profile, sensitiveGate, owner => new WindowsProfileTransferPrompts(owner));
            profileWindow.Show();
            _ = profileWindow.Activate();
        }

        BitwardenUnlockWindow? unlockWindow = null;
        void OpenBitwardenUnlock()
        {
            if (unlockWindow is { IsVisible: true })
            {
                _ = unlockWindow.Activate();
                return;
            }

            if (bitwarden.IsUnlocked)
            {
                bitwarden.Lock();
                notifier.Notify("Bitwarden verrouillé : PasteFlow ne remplit plus les mots de passe.");
                return;
            }

            if (!bitwarden.IsInstalled)
            {
                notifier.Notify("Bitwarden en ligne de commande n'est pas installé. Dans un terminal : winget install Bitwarden.CLI (ou npm install -g @bitwarden/cli), puis bw login.");
                return;
            }

            unlockWindow = new BitwardenUnlockWindow(async password =>
            {
                var isUnlocked = await bitwarden.UnlockAsync(password, CancellationToken.None);
                if (isUnlocked)
                {
                    notifier.Notify("Bitwarden déverrouillé. Sur une page de connexion, clique dans l'identifiant puis Ctrl+Alt+V.");
                    await ImportBitwardenIdentityAsync(bitwarden, learner, notifier);
                }

                return isUnlocked;
            });
            unlockWindow.Show();
            _ = unlockWindow.Activate();
        }

        void ClearHistoryAfterAsking()
        {
            // From the keyboard, a letter typed by mistake must not erase what PasteFlow learned
            var answer = MessageBox.Show(
                "Effacer l'historique ? PasteFlow oubliera les valeurs apprises de tes remplissages. Ta fiche, elle, est gardée.",
                "PasteFlow",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (answer == MessageBoxResult.Yes)
            {
                _ = ClearHistoryAsync(history, notifier);
            }
        }

        async Task UndoLastLearnedAsync()
        {
            notifier.Notify(await learningPrompts.UndoLastAsync()
                ? "Dernier ajout retiré de ta fiche."
                : "Rien à retirer : aucun ajout récent à ta fiche.");
        }

        MenuWindow? menuWindow = null;
        void OpenMenu()
        {
            if (menuWindow is { IsVisible: true })
            {
                _ = menuWindow.Activate();
                return;
            }

            menuWindow = new MenuWindow(new MenuEntry[]
            {
                new('F', "Ma fiche…", OpenProfile),
                new('A', "Annuler le dernier ajout à ma fiche", () => _ = UndoLastLearnedAsync(), learningPrompts.CanUndoLast),
                new('B', bitwarden.IsUnlocked ? "Verrouiller Bitwarden" : "Déverrouiller Bitwarden…", OpenBitwardenUnlock),
                new('P', "Paramètres…", OpenSettings),
                new('H', "Effacer l'historique…", ClearHistoryAfterAsking),
                new('Q', "Quitter PasteFlow", application.Shutdown),
            });
            menuWindow.Show();
            _ = menuWindow.Activate();
        }

        Apply(settings);
        using var trayIcon = new TrayIcon(application.Shutdown, () => _ = ClearHistoryAsync(history, notifier), OpenSettings, OpenProfile, OpenBitwardenUnlock, () => bitwarden.IsUnlocked);

        // Everything the icon offers, with the keyboard
        var menuHotkey = Hotkey.DefaultMenu;
        var isMenuReady = menuHotkeyListener.TryRegister(menuHotkey);
        menuHotkeyListener.HotkeyPressed += (_, _) => OpenMenu();

        // A value seen on screen pasted by typing the letters put on it, as Vimium does
        using var hints = new HintService(labelsHotkeyListener, new KeyboardTextInserter(), notifier);
        _ = hints.Start(Hotkey.DefaultLabels);

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
            notifier.Notify(isMenuReady
                ? $"PasteFlow est lancé. Appuie sur {hotkey} pour le tester, {menuHotkey} pour son menu."
                : $"PasteFlow est lancé. Appuie sur {hotkey} pour le tester.");
        }
        else
        {
            trayIcon.ShowStatus("raccourci indisponible");
            notifier.Notify($"Le raccourci {hotkey} est déjà utilisé par une autre application.");
        }

        _ = application.Dispatcher.InvokeAsync(async () =>
        {
            await StartProfileAsync(learner, notifier);

            // At launch, a locked vault asks for its master password once; "Plus tard" leaves it for the tray menu
            if (bitwarden.IsInstalled && await bitwarden.StatusAsync(CancellationToken.None) == "locked")
            {
                OpenBitwardenUnlock();
            }
        });

        // The official lists are loaded now, in the background, rather than at the first fill
        _ = Task.Run(() => ReferenceData.Shared);

        return application.Run();
    }

    /// <summary>
    /// At the very first launch, starts the profile with the name and the email of the Windows account.
    /// </summary>
    /// <param name="learner">The learner that fills the profile.</param>
    /// <param name="notifier">The notifier that tells the user.</param>
    /// <returns>A task completed once the profile is started.</returns>
    private static async Task StartProfileAsync(ProfileLearner learner, PopupNotifier notifier)
    {
        var marker = Path.Combine(Path.GetDirectoryName(ProtectedFileProfileStore.DefaultPath)!, "profile-started");
        if (File.Exists(marker))
        {
            return;
        }

        var added = await learner.ImportWindowsAccountAsync(await WindowsAccountReader.ReadAsync(), CancellationToken.None);
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            await File.WriteAllTextAsync(marker, "Fiche démarrée avec le compte Windows.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Without the marker, the next launch tries again; the values already there are not added twice
        }

        if (added > 0)
        {
            notifier.Notify("Ta fiche démarre avec ton nom et ton e-mail Windows. Clic droit sur l'icône PasteFlow, « Ma fiche… », pour la compléter.");
        }
    }

    /// <summary>
    /// The first time Bitwarden is unlocked, adds its identity to the profile: ordinary values at once, sensitive ones
    /// only if the user agrees, behind Windows Hello.
    /// </summary>
    /// <param name="bitwarden">The unlocked Bitwarden.</param>
    /// <param name="learner">The learner that fills the profile.</param>
    /// <param name="notifier">The notifier that tells the user.</param>
    /// <returns>A task completed once the identity is imported.</returns>
    private static async Task ImportBitwardenIdentityAsync(BitwardenCli bitwarden, ProfileLearner learner, PopupNotifier notifier)
    {
        var marker = Path.Combine(Path.GetDirectoryName(ProtectedFileProfileStore.DefaultPath)!, "bitwarden-identity-imported");
        if (File.Exists(marker))
        {
            return;
        }

        var identity = await bitwarden.GetIdentityAsync(CancellationToken.None);
        var added = await learner.ImportIdentityAsync(identity, ProfileOrigin.PasswordManager, CancellationToken.None);
        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            await File.WriteAllTextAsync(marker, "Identité Bitwarden reprise dans la fiche.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Without the marker, the next unlock tries again; the values already there are not added twice
        }

        if (added > 0)
        {
            notifier.Notify($"Ton identité Bitwarden a complété ta fiche ({added} infos).");
        }
    }

    private static async Task ClearHistoryAsync(FillHistory history, PopupNotifier notifier)
    {
        await history.ClearAsync(CancellationToken.None);
        notifier.Notify("Historique effacé : PasteFlow a oublié les valeurs apprises.");
    }
}
