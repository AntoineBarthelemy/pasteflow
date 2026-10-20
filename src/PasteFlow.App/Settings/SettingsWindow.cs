using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PasteFlow.App.Design;
using PasteFlow.App.Updates;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Settings;

namespace PasteFlow.App.Settings;

/// <summary>
/// Window where the user chooses the prudence of the fills, the learning and the start with Windows, in the way of the
/// settings of a Mac: groups of rows on white cards, switches, and the shortcuts as keys.
/// </summary>
internal sealed class SettingsWindow : ThemedWindow
{
    private const double ContentWidth = 500;

    private static readonly string[] PrudenceDescriptions =
    {
        "Ne remplit que ce qui est quasi certain, propose le reste.",
        "Recommandé : sûr sans être excessif.",
        "Remplit davantage, à vérifier plus souvent.",
    };

    private readonly Action<AppSettings> _saved;
    private readonly RadioButton _cautious;
    private readonly RadioButton _balanced;
    private readonly RadioButton _bold;
    private readonly TextBlock _prudenceDescription;
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

        _cautious = Segment("Prudent", current.Prudence == Prudence.Cautious);
        _balanced = Segment("Équilibré", current.Prudence == Prudence.Balanced);
        _bold = Segment("Audacieux", current.Prudence == Prudence.Bold);
        _prudenceDescription = Ui.Text(string.Empty, TextRole.Caption);
        foreach (var choice in new[] { _cautious, _balanced, _bold })
        {
            choice.Checked += (_, _) => ShowPrudence();
        }

        _learn = Switch("Apprendre de mes remplissages", current.LearnFromFills);
        _readImages = Switch("Lire l'image des fenêtres qui ne donnent pas leur texte", current.ReadImages);
        _startWithWindows = Switch("Lancer PasteFlow au démarrage de Windows", current.StartWithWindows);

        var save = Ui.ActionButton("Enregistrer", isDefault: true);
        save.Click += (_, _) => Save();
        var cancel = Ui.ActionButton("Annuler", isCancel: true);
        cancel.Click += (_, _) => Close();

        var header = Ui.Header(Icons.Settings, "Paramètres", "Comment PasteFlow remplit, apprend et démarre.");
        header.Margin = new Thickness(0, 0, 0, 4);
        var page = new StackPanel { Width = ContentWidth };
        page.Children.Add(SectionTitle("Remplissage"));
        var segments = new StackPanel { Orientation = Orientation.Horizontal };
        segments.Children.Add(_cautious);
        segments.Children.Add(_balanced);
        segments.Children.Add(_bold);
        var prudence = new StackPanel();
        prudence.Children.Add(new Border
        {
            Background = Theme.Brush(palette => palette.Sunken),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = segments,
        });
        _prudenceDescription.Margin = new Thickness(2, 8, 0, 0);
        prudence.Children.Add(_prudenceDescription);
        page.Children.Add(Ui.Card(Row("Prudence", string.Empty, prudence, stacked: true), new Thickness(16, 14, 16, 14)));

        page.Children.Add(SectionTitle("Lecture et apprentissage"));
        var reading = new StackPanel();
        reading.Children.Add(Row("Apprendre de mes remplissages", "Historique chiffré, gardé sur ce PC.", _learn, stacked: false));
        reading.Children.Add(Ui.Divider(new Thickness(0, 12, 0, 12)));
        reading.Children.Add(Row("Lire l'image des fenêtres", "Pour celles qui ne donnent pas leur texte. Les valeurs lues sont seulement proposées.", _readImages, stacked: false));
        reading.Children.Add(Ui.Divider(new Thickness(0, 12, 0, 12)));
        reading.Children.Add(Row("Lancer au démarrage de Windows", "PasteFlow attend dans la barre des tâches.", _startWithWindows, stacked: false));
        page.Children.Add(Ui.Card(reading, new Thickness(16, 14, 16, 14)));

        page.Children.Add(SectionTitle("Raccourcis"));
        var shortcuts = new StackPanel();
        AddShortcut(shortcuts, "Remplir, ou coller au curseur", "Ctrl+Alt+V", isFirst: true);
        AddShortcut(shortcuts, "Annuler le dernier remplissage", "Ctrl+Alt+Z");
        AddShortcut(shortcuts, "Choisir une proposition", "Ctrl+Maj+1 à 3");
        AddShortcut(shortcuts, "Lettres sur les valeurs de l'écran", "Ctrl+Alt+F");
        AddShortcut(shortcuts, "Menu de PasteFlow", "Ctrl+Alt+P");
        page.Children.Add(Ui.Card(shortcuts, new Thickness(16, 10, 16, 10)));

        var footer = new DockPanel { Margin = new Thickness(0, 18, 0, 0) };
        var buttons = Ui.ButtonRow(cancel, save);
        buttons.Margin = new Thickness(0);
        DockPanel.SetDock(buttons, Dock.Right);
        footer.Children.Add(buttons);
        var version = Ui.Line($"PasteFlow {AppUpdates.CurrentVersion}", TextRole.Hint);
        version.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(version);
        SetPage(header, page, footer);
        ShowPrudence();

        // The keyboard starts on the prudence chosen: the arrows change it, Tab goes on, Entrée saves
        Loaded += (_, _) => _ = new[] { _cautious, _balanced, _bold }.FirstOrDefault(choice => choice.IsChecked == true, _balanced).Focus();
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

    private static RadioButton Segment(string text, bool isChecked)
    {
        var segment = new RadioButton { Content = text, GroupName = "Prudence", IsChecked = isChecked };
        segment.SetResourceReference(StyleProperty, "Pf.Segment");
        return segment;
    }

    private static CheckBox Switch(string name, bool isChecked)
    {
        var box = new CheckBox { IsChecked = isChecked, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(box, name);
        return box;
    }

    private static TextBlock SectionTitle(string text)
    {
        var title = Ui.Text(text, TextRole.Caption);
        title.FontWeight = FontWeights.SemiBold;
        title.Margin = new Thickness(4, 18, 0, 8);
        return title;
    }

    private static Grid Row(string label, string description, FrameworkElement control, bool stacked)
    {
        // The control comes first in the tree, so that screen readers meet the switch before its words
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        texts.Children.Add(Ui.Text(label, TextRole.Body));
        if (description.Length > 0)
        {
            var line = Ui.Text(description, TextRole.Caption);
            line.Margin = new Thickness(0, 2, 0, 0);
            texts.Children.Add(line);
        }

        if (stacked)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            control.Margin = new Thickness(0, 10, 0, 0);
            Grid.SetRow(control, 1);
            Grid.SetColumnSpan(control, 2);
        }
        else
        {
            Grid.SetColumn(control, 1);
        }

        grid.Children.Add(control);
        grid.Children.Add(texts);
        return grid;
    }

    private static void AddShortcut(StackPanel list, string action, string keys, bool isFirst = false)
    {
        if (!isFirst)
        {
            list.Children.Add(Ui.Divider(new Thickness(0, 8, 0, 8)));
        }

        var row = new DockPanel();
        var keycaps = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var key in keys.Split('+'))
        {
            var cap = Ui.Keycap(key);
            cap.Margin = new Thickness(4, 0, 0, 0);
            keycaps.Children.Add(cap);
        }

        DockPanel.SetDock(keycaps, Dock.Right);
        row.Children.Add(keycaps);
        var text = Ui.Text(action, TextRole.Body);
        text.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(text);
        list.Children.Add(row);
    }

    private void ShowPrudence()
    {
        var index = _cautious.IsChecked == true ? 0 : _bold.IsChecked == true ? 2 : 1;
        _prudenceDescription.Text = PrudenceDescriptions[index];
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
