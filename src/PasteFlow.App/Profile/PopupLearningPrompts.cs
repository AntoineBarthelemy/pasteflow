using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PasteFlow.App.Design;
using PasteFlow.App.Notifications;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Profile;

/// <summary>
/// Tells the user what PasteFlow added to their profile, in a popup with an "Annuler" button, and asks before keeping
/// a sensitive value.
/// </summary>
internal sealed class PopupLearningPrompts : IProfileLearningPrompts
{
    // The recap of the fill is shown first: the news about the profile comes once it had time to be read
    private static readonly TimeSpan DelayAfterFill = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DisplayTime = TimeSpan.FromSeconds(8);

    private readonly PopupNotifier _notifier;
    private readonly Dispatcher _dispatcher;
    private Func<Task>? _lastUndo;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopupLearningPrompts"/> class.
    /// </summary>
    /// <param name="notifier">The notifier that shows the popups, one at a time.</param>
    /// <param name="dispatcher">The dispatcher of the user interface thread.</param>
    public PopupLearningPrompts(PopupNotifier notifier, Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _notifier = notifier;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Gets a value indicating whether the last value added to the profile can still be taken back.
    /// </summary>
    public bool CanUndoLast => Volatile.Read(ref _lastUndo) is not null;

    /// <inheritdoc/>
    public void ShowAdded(ProfileValue value, Func<Task> undo)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(undo);

        // The menu of PasteFlow can take it back too, without the mouse
        Volatile.Write(ref _lastUndo, undo);
        _ = _dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(DelayAfterFill);

            // An icon of the profile, the sentence, and a quiet "Annuler" on the right
            var icon = Ui.Icon(Icons.Person, 16, Theme.Brush(palette => palette.Accent));
            icon.Margin = new Thickness(0, 0, 12, 0);
            var text = Ui.Text(AddedSentence(value), TextRole.Body);
            text.MaxWidth = 340;
            text.VerticalAlignment = VerticalAlignment.Center;
            var cancel = Ui.QuietButton("Annuler");
            cancel.Margin = new Thickness(12, 0, -6, 0);
            cancel.MinHeight = 28;
            cancel.Foreground = Theme.Brush(palette => palette.Accent);
            var panel = new DockPanel();
            DockPanel.SetDock(icon, Dock.Left);
            DockPanel.SetDock(cancel, Dock.Right);
            panel.Children.Add(icon);
            panel.Children.Add(cancel);
            panel.Children.Add(text);
            cancel.Click += async (_, _) =>
            {
                cancel.IsEnabled = false;
                _ = Interlocked.CompareExchange(ref _lastUndo, null, undo);
                await undo();
                text.Text = "Retiré de ta fiche.";
            };
            _notifier.ShowContent(panel, DisplayTime);
        });
    }

    /// <summary>
    /// Takes back the last value added to the profile, once.
    /// </summary>
    /// <returns><see langword="true"/> if a value was taken back; otherwise <see langword="false"/>.</returns>
    public async Task<bool> UndoLastAsync()
    {
        if (Interlocked.Exchange(ref _lastUndo, null) is not { } undo)
        {
            return false;
        }

        await undo();
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> AskToAddSensitiveAsync(ProfileValue value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        // Shown in front and with the keyboard: the question comes a few seconds after a fill, from the background
        return _dispatcher.InvokeAsync(
            () => ConfirmWindow.Ask(Icons.Shield, "Une info sensible", SensitiveQuestion(value), "Ajouter à ma fiche", "Pas maintenant"),
            DispatcherPriority.Normal,
            cancellationToken).Task;
    }

    /// <summary>
    /// Gets the sentence that tells a value was added.
    /// </summary>
    /// <param name="value">The value added.</param>
    /// <returns>The sentence, in French, with the value hidden if it is sensitive.</returns>
    internal static string AddedSentence(ProfileValue value)
    {
        return $"Ajouté à ta fiche : {value.ShownLabel}, {value.ShownValue}";
    }

    /// <summary>
    /// Gets the question asked before keeping a sensitive value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The question, in French, with the value hidden.</returns>
    internal static string SensitiveQuestion(ProfileValue value)
    {
        var seen = value.Origin == ProfileOrigin.PasswordManager
            ? $"Ton identité Bitwarden contient ton {value.ShownLabel} ({value.ShownValue})."
            : $"Tu as rempli deux fois ton {value.ShownLabel} ({value.ShownValue}).";
        return seen + "\n\nL'ajouter à ta fiche, chiffrée sur ce PC ? Windows Hello te sera demandé pour l'utiliser.";
    }
}
