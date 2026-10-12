using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

    /// <inheritdoc/>
    public void ShowAdded(ProfileValue value, Func<Task> undo)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(undo);

        _ = _dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(DelayAfterFill);
            var text = new TextBlock
            {
                Text = AddedSentence(value),
                Foreground = PopupColors.Text,
                FontSize = 14,
                MaxWidth = 360,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var cancel = new Button { Content = "Annuler", Margin = new Thickness(12, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2) };
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(text);
            panel.Children.Add(cancel);
            cancel.Click += async (_, _) =>
            {
                cancel.IsEnabled = false;
                await undo();
                text.Text = "Retiré de ta fiche.";
            };
            _notifier.ShowContent(panel, DisplayTime);
        });
    }

    /// <inheritdoc/>
    public Task<bool> AskToAddSensitiveAsync(ProfileValue value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        return _dispatcher.InvokeAsync(
            () => MessageBox.Show(SensitiveQuestion(value), "PasteFlow", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes,
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
