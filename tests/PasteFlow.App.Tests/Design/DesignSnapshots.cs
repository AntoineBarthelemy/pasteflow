using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PasteFlow.App.Design;
using PasteFlow.App.Highlights;
using PasteFlow.App.Logins;
using PasteFlow.App.Notifications;
using PasteFlow.App.Picking;
using PasteFlow.App.Profile;
using PasteFlow.App.Settings;
using PasteFlow.App.Tests.Fakes;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.App.Tray;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Logins;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Security;
using PasteFlow.Core.Settings;

namespace PasteFlow.App.Tests.Design;

/// <summary>
/// Pictures of every window and popup of PasteFlow, in the light and dark themes, to look at the design without a
/// screen: only run on demand by the "Snapshots" workflow, which sets PASTEFLOW_SNAPSHOTS to the folder of the pictures.
/// </summary>
public sealed class DesignSnapshots
{
    private static readonly string? Folder = Environment.GetEnvironmentVariable("PASTEFLOW_SNAPSHOTS");
    private static readonly string Themes = Environment.GetEnvironmentVariable("PASTEFLOW_SNAPSHOT_THEMES") ?? "clair,sombre";
    private static readonly string Group = Environment.GetEnvironmentVariable("PASTEFLOW_SNAPSHOT_GROUP") ?? "all";

    // The pictures are shared out in groups, so that each job of the workflow stays under the limits of GitHub for
    // annotations (about 50 per job)
    private static readonly Dictionary<string, string> Groups = new()
    {
        ["parametres"] = "1",
        ["notification"] = "1",
        ["fiche"] = "2",
        ["bitwarden"] = "3",
        ["comptes"] = "3",
        ["mot-de-passe"] = "3",
        ["confirmation"] = "4",
        ["menu"] = "4",
        ["bulle"] = "4",
        ["erreur"] = "4",
        ["liste"] = "5",
        ["recapitulatif"] = "5",
    };

    [SkippableFact]
    public void Render_EveryWindowAndPopup()
    {
        Skip.If(string.IsNullOrEmpty(Folder), "Les captures ne sont faites qu'à la demande.");
        _ = Directory.CreateDirectory(Folder!);

        try
        {
            foreach (var theme in Themes.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                Theme.ForcedDark = theme == "sombre";
                RenderAll(theme);
            }
        }
        finally
        {
            Theme.ForcedDark = null;
        }
    }

    private static void RenderAll(string theme)
    {
        Snap(theme, "parametres", () => new SettingsWindow(new AppSettings { LearnFromFills = true, ReadImages = true }, _ => { }));
        Snap(theme, "fiche", () => new ProfileWindow(Book(), new SensitiveGate(new FakePresenceCheck()), _ => new FakeProfileTransferPrompts()));
        Snap(theme, "bitwarden", () => new BitwardenUnlockWindow(_ => Task.FromResult(false)));
        Snap(theme, "comptes", () => new LoginPickerWindow(new[]
        {
            new VaultLogin { Id = "1", Name = "GitHub", UserName = "antoine.b@orange.fr" },
            new VaultLogin { Id = "2", Name = "GitHub (école)", UserName = "antoine.barthelemy@etudiant.univ-reims.fr" },
        }));
        Snap(theme, "mot-de-passe", () => new PasswordPromptWindow(isNew: true));
        Snap(theme, "confirmation", () => new ConfirmWindow(Icons.History, "Effacer l'historique ?", "PasteFlow oubliera les valeurs apprises de tes remplissages. Ta fiche, elle, est gardée.", "Effacer", "Annuler", isDestructive: true));
        Snap(theme, "menu", () => new MenuWindow(new MenuEntry[]
        {
            new('F', "Ma fiche…", () => { }, Icon: Icons.Person),
            new('A', "Annuler le dernier ajout à ma fiche", () => { }, IsEnabled: false, Icon: Icons.Undo),
            new('B', "Verrouiller Bitwarden", () => { }, Icon: Icons.Lock),
            new('P', "Paramètres…", () => { }, Icon: Icons.Settings),
            new('H', "Effacer l'historique…", () => { }, Icon: Icons.History),
            new('U', "Rechercher une mise à jour", () => { }, Icon: Icons.Download),
            new('J', "Journal des erreurs…", () => { }, Icon: Icons.Warning),
            new('Q', "Quitter PasteFlow", () => { }, Icon: Icons.Power),
        }));
        Snap(theme, "erreur", () => new NotificationPopup(ErrorNotice.Create(), ErrorNotice.DisplayTime));
        Snap(theme, "notification", () => new NotificationPopup("Bitwarden déverrouillé. Sur une page de connexion, clique dans l'identifiant puis Ctrl+Alt+V."));
        Snap(theme, "recapitulatif", () => new NotificationPopup(RecapView.Create(FillRecap.From(Report())), TimeSpan.FromSeconds(30)));
        Snap(theme, "bulle", () =>
        {
            var bubble = new SuggestionBubble();
            bubble.SetSuggestions(
                new[] { new SuggestedValue { Value = "lea.martin@atelier-nova.fr", SourceName = "Claude" }, new SuggestedValue { Value = "hugo@atelier-nova.fr", SourceName = "Claude" } },
                new[] { "Ctrl+Maj+1", "Ctrl+Maj+2" },
                "Deux valeurs aussi probables : « lea.martin@atelier-nova.fr » et « hugo@atelier-nova.fr ».");
            return bubble;
        });
        Snap(theme, "liste", () =>
        {
            var suggestions = new[]
            {
                new Candidate { Value = "lea.martin@atelier-nova.fr", Kind = CandidateKind.Email, SourceName = "Claude", SourceRank = 1 },
                new Candidate { Value = "06 12 48 30 77", Kind = CandidateKind.Phone, SourceName = "Claude", SourceRank = 1 },
                new Candidate { Value = "hugo@atelier-nova.fr", Kind = CandidateKind.Email, SourceName = "Claude", SourceRank = 1 },
            };
            var picker = new PastePickerWindow(new PickRequest
            {
                Suggestions = suggestions,
                Finder = new PassageFinder(new[] { new WindowText { Title = "Claude", Rank = 1, Text = "Léa Martin" } }, suggestions),
            });
            picker.CancelsWhenDeactivated = false;
            return picker;
        });
    }

    private static ProfileBook Book()
    {
        return new ProfileBook(new InMemoryProfileStore(
            new ProfileValue { Topic = ProfileTopic.FirstName, Value = "Antoine" },
            new ProfileValue { Topic = ProfileTopic.LastName, Value = "Barthélémy" },
            new ProfileValue { Topic = ProfileTopic.Email, Value = "antoine.b@orange.fr", Label = "Perso" },
            new ProfileValue { Topic = ProfileTopic.City, Value = "Taissy", Origin = ProfileOrigin.Learned },
            new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189" },
            new ProfileValue { Topic = ProfileTopic.Other, Value = "Anglais B2, français langue maternelle", Label = "Langues" }));
    }

    private static FillReport Report()
    {
        return new FillReport
        {
            FilledCount = 3,
            UncertainCount = 1,
            AlreadyFilledCount = 0,
            FailedCount = 0,
            Sources = new[] { new SourceSummary { Name = "Mail de Marie Lefèvre", CandidateCount = 9 } },
            Elapsed = TimeSpan.FromMilliseconds(184),
            UndoHotkey = PasteFlow.Core.Hotkeys.Hotkey.DefaultUndo,
            FilledValues = new[]
            {
                new FilledValue { FieldLabel = "Prénom", Value = "Marie", SourceName = "Mail de Marie Lefèvre" },
                new FilledValue { FieldLabel = "E-mail", Value = "marie.lefevre@boulangerie-martin.fr", SourceName = "Mail de Marie Lefèvre" },
                new FilledValue { FieldLabel = "Téléphone", Value = "0326472185", SourceName = "Mail de Marie Lefèvre" },
            },
            FieldsToCheck = new[]
            {
                new FieldToCheck
                {
                    Field = new FormField { Id = "cp", Label = "Code postal", Kind = FieldKind.Text, HasValue = false },
                    Suggestions = new[] { new SuggestedValue { Value = "51500", SourceName = "Bon de livraison" } },
                    Reason = "51500 ne correspond pas à Reims selon La Poste.",
                },
            },
        };
    }

    private static void Snap(string theme, string name, Func<Window> create)
    {
        if (Group != "all" && Groups.GetValueOrDefault(name, "1") != Group)
        {
            return;
        }

        using var host = new WindowHost<Window>(create);
        Thread.Sleep(600);
        host.Invoke(window =>
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            Save(window, Path.Combine(Folder!, $"{theme}-{name}.png"));
            return true;
        });
    }

    private static void Save(Window window, string path)
    {
        var root = window.Content as FrameworkElement ?? window;
        var width = (int)Math.Ceiling(root.ActualWidth + root.Margin.Left + root.Margin.Right);
        var height = (int)Math.Ceiling(root.ActualHeight + root.Margin.Top + root.Margin.Bottom);
        var picture = new RenderTargetBitmap(Math.Max(width, 1), Math.Max(height, 1), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // Behind transparent popups, the background of the theme, so that their shadow can be seen
            var background = window.Background is SolidColorBrush { Color.A: 255 } solid ? solid : Theme.Brush(palette => palette.Background);
            context.DrawRectangle(background, null, new Rect(0, 0, width, height));

            // The whole window, margin included: the shadow of a popup spreads into it. The coordinates of the brush
            // already start at the window, before the margin
            var area = new Rect(0, 0, width, height);
            var content = new VisualBrush(root)
            {
                Stretch = Stretch.None,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = area,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = area,
            };
            context.DrawRectangle(content, null, area);
        }

        picture.Render(visual);

        // 256 colors are enough for an interface and make the picture several times lighter
        var indexed = new FormatConvertedBitmap(picture, PixelFormats.Indexed8, new BitmapPalette(picture, 256), 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(indexed));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
