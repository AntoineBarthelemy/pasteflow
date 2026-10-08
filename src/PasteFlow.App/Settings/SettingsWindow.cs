using System.Windows;
using System.Windows.Controls;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Settings;

namespace PasteFlow.App.Settings;

/// <summary>
/// Window where the user chooses the prudence of the fills, the learning and the start with Windows.
/// </summary>
internal sealed class SettingsWindow : Window
{
    private readonly Action<AppSettings> _saved;
    private readonly RadioButton _cautious;
    private readonly RadioButton _bold;
    private readonly CheckBox _learn;
    private readonly CheckBox _readImages;
    private readonly CheckBox _startWithWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="current">The settings shown when the window opens.</param>
    /// <param name="saved">The action run with the new settings when the user saves them.</param>
    public SettingsWindow(AppSettings current, Action<AppSettings> saved)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(saved);

        _saved = saved;
        Title = "Paramètres de PasteFlow";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _cautious = Choice("Prudent : ne remplit que ce qui est quasi certain, propose le reste", current.Prudence == Prudence.Cautious);
        var balanced = Choice("Équilibré (recommandé) : prudent sans être excessif", current.Prudence == Prudence.Balanced);
        _bold = Choice("Audacieux : remplit davantage, à vérifier plus souvent", current.Prudence == Prudence.Bold);
        _learn = new CheckBox
        {
            Content = "Apprendre de mes remplissages (historique chiffré, gardé sur ce PC)",
            IsChecked = current.LearnFromFills,
            Margin = new Thickness(0, 4, 0, 4),
        };
        _readImages = new CheckBox
        {
            Content = "Lire l'image des fenêtres qui ne donnent pas leur texte (valeurs seulement proposées)",
            IsChecked = current.ReadImages,
            Margin = new Thickness(0, 4, 0, 4),
        };
        _startWithWindows = new CheckBox
        {
            Content = "Lancer PasteFlow au démarrage de Windows",
            IsChecked = current.StartWithWindows,
            Margin = new Thickness(0, 4, 0, 4),
        };

        var save = new Button { Content = "Enregistrer", IsDefault = true, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
        save.Click += (_, _) => Save();
        var cancel = new Button { Content = "Annuler", IsCancel = true, MinWidth = 100 };
        cancel.Click += (_, _) => Close();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(save);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(20), MaxWidth = 480 };
        panel.Children.Add(Heading("Prudence du remplissage"));
        panel.Children.Add(_cautious);
        panel.Children.Add(balanced);
        panel.Children.Add(_bold);
        panel.Children.Add(Heading("Lecture, apprentissage et démarrage"));
        panel.Children.Add(_learn);
        panel.Children.Add(_readImages);
        panel.Children.Add(_startWithWindows);
        panel.Children.Add(Heading("Raccourcis"));
        panel.Children.Add(new TextBlock
        {
            Text = "Ctrl+Alt+V remplit le formulaire, Ctrl+Alt+Z annule le dernier remplissage, "
                + "Ctrl+Maj+1, 2 ou 3 choisit une proposition dans un champ encadré en orange.",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(buttons);
        Content = panel;
    }

    /// <summary>
    /// Gets the settings chosen in the window.
    /// </summary>
    internal AppSettings Chosen => new()
    {
        Prudence = PrudenceChosen(),
        LearnFromFills = _learn.IsChecked == true,
        ReadImages = _readImages.IsChecked == true,
        StartWithWindows = _startWithWindows.IsChecked == true,
    };

    private static RadioButton Choice(string text, bool isChecked)
    {
        return new RadioButton { Content = text, GroupName = "Prudence", IsChecked = isChecked, Margin = new Thickness(0, 4, 0, 4) };
    }

    private static TextBlock Heading(string text)
    {
        return new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) };
    }

    private Prudence PrudenceChosen()
    {
        if (_cautious.IsChecked == true)
        {
            return Prudence.Cautious;
        }

        return _bold.IsChecked == true ? Prudence.Bold : Prudence.Balanced;
    }

    private void Save()
    {
        _saved(Chosen);
        Close();
    }
}
