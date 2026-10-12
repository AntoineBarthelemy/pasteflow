using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Profile;

/// <summary>
/// Window where the user sees their profile, adds, corrects and removes informations, and exports or imports it.
/// </summary>
/// <remarks>Sensitive values are never shown whole: only their last characters, and they are retyped to be corrected.</remarks>
internal sealed class ProfileWindow : Window
{
    // An export holds a few hundred values at most: anything far bigger is not one
    private const int MaxArchiveBytes = 1024 * 1024;

    private readonly ProfileBook _book;
    private readonly IProfileTransferPrompts _prompts;
    private readonly ListBox _list;
    private readonly ComboBox _topic;
    private readonly TextBox _value;
    private readonly TextBox _label;
    private readonly Button _correct;
    private readonly Button _remove;
    private readonly TextBlock _status;
    private IReadOnlyList<ProfileValue> _values = Array.Empty<ProfileValue>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileWindow"/> class.
    /// </summary>
    /// <param name="book">The profile shown and changed.</param>
    /// <param name="createPrompts">Creates, for this window, what asks where to export or import and the password.</param>
    public ProfileWindow(ProfileBook book, Func<Window, IProfileTransferPrompts> createPrompts)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(createPrompts);

        _book = book;
        _prompts = createPrompts(this);
        Title = "Ma fiche PasteFlow";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _list = new ListBox { Height = 260, Margin = new Thickness(0, 4, 0, 8) };
        AutomationProperties.SetName(_list, "Infos de ma fiche");
        _list.SelectionChanged += (_, _) => ShowSelected();

        _topic = new ComboBox { ItemsSource = ProfileTopics.All.Select(ProfileTopics.DisplayName).ToList(), SelectedIndex = 0, Margin = new Thickness(0, 2, 0, 6) };
        AutomationProperties.SetName(_topic, "Sujet");
        _value = new TextBox { Margin = new Thickness(0, 2, 0, 6) };
        AutomationProperties.SetName(_value, "Valeur");
        _label = new TextBox { Margin = new Thickness(0, 2, 0, 6) };
        AutomationProperties.SetName(_label, "Nom (facultatif)");

        var add = ActionButton("Ajouter");
        add.Click += async (_, _) => await AddAsync();
        _correct = ActionButton("Corriger");
        _correct.Click += async (_, _) => await CorrectAsync();
        _remove = ActionButton("Supprimer");
        _remove.Click += async (_, _) => await RemoveAsync();
        var export = ActionButton("Exporter…");
        export.Click += async (_, _) => await ExportAsync();
        var import = ActionButton("Importer…");
        import.Click += async (_, _) => await ImportAsync();
        var close = ActionButton("Fermer");
        close.IsCancel = true;
        close.Click += (_, _) => Close();

        _status = new TextBlock { TextWrapping = TextWrapping.Wrap, MinHeight = 36, Margin = new Thickness(0, 8, 0, 0) };
        AutomationProperties.SetName(_status, "État");

        var panel = new StackPanel { Margin = new Thickness(20), Width = 460 };
        panel.Children.Add(new TextBlock
        {
            Text = "Les infos que PasteFlow peut écrire pour toi. Elles restent chiffrées sur ce PC. "
                + "Les infos sensibles (IBAN, n° de sécu…) ne sont jamais affichées en entier.",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(_list);
        panel.Children.Add(new TextBlock { Text = "Sujet" });
        panel.Children.Add(_topic);
        panel.Children.Add(new TextBlock { Text = "Valeur" });
        panel.Children.Add(_value);
        panel.Children.Add(new TextBlock { Text = "Nom (facultatif, par exemple « E-mail de l'IUT »)" });
        panel.Children.Add(_label);
        panel.Children.Add(Row(add, _correct, _remove));
        panel.Children.Add(_status);
        panel.Children.Add(Row(export, import, close));
        Content = panel;

        ShowSelected();
        Loaded += async (_, _) => await RefreshAsync();
    }

    /// <summary>
    /// Gets the lines of the list, as the user sees them.
    /// </summary>
    internal IReadOnlyList<string> Lines => _list.Items.Cast<ListBoxItem>().Select(item => (string)item.Content).ToList();

    /// <summary>
    /// Gets the last sentence shown to the user.
    /// </summary>
    internal string Status => _status.Text;

    private ProfileValue? Selected => _list.SelectedIndex >= 0 && _list.SelectedIndex < _values.Count ? _values[_list.SelectedIndex] : null;

    private ProfileTopic TopicChosen => ProfileTopics.All[Math.Max(_topic.SelectedIndex, 0)];

    /// <summary>
    /// Gets the line of a value in the list: its name and the value, hidden if it is sensitive, then where it comes from.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The line.</returns>
    internal static string Line(ProfileValue value)
    {
        var origin = value.Origin switch
        {
            ProfileOrigin.Learned => " (appris)",
            ProfileOrigin.WindowsAccount => " (compte Windows)",
            ProfileOrigin.PasswordManager => " (Bitwarden)",
            _ => string.Empty,
        };
        return $"{value.ShownLabel} : {value.ShownValue}{origin}";
    }

    /// <summary>
    /// Chooses the topic of the value typed, as the user does with the list of topics.
    /// </summary>
    /// <param name="topic">The topic.</param>
    internal void ChooseTopic(ProfileTopic topic)
    {
        _topic.SelectedIndex = ProfileTopics.All.ToList().IndexOf(topic);
    }

    private static Button ActionButton(string text)
    {
        return new Button { Content = text, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
    }

    private static StackPanel Row(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }

        return row;
    }

    private async Task RefreshAsync()
    {
        var selectedId = Selected?.Id;
        _values = await _book.GetValuesAsync(CancellationToken.None);
        _list.Items.Clear();
        foreach (var value in _values)
        {
            var line = Line(value);
            var item = new ListBoxItem { Content = line };
            AutomationProperties.SetName(item, line);
            _ = _list.Items.Add(item);
        }

        var index = _values.ToList().FindIndex(value => value.Id == selectedId);
        _list.SelectedIndex = index;
        ShowSelected();
    }

    private void ShowSelected()
    {
        var selected = Selected;
        _correct.IsEnabled = selected is not null;
        _remove.IsEnabled = selected is not null;
        if (selected is null)
        {
            return;
        }

        _topic.SelectedIndex = ProfileTopics.All.ToList().IndexOf(selected.Topic);
        _label.Text = selected.Label;

        // A sensitive value is never put back on screen: the user retypes it to correct it
        _value.Text = selected.IsSensitive ? string.Empty : selected.Value;
        _status.Text = selected.IsSensitive ? "Valeur sensible masquée : retape-la en entier pour la corriger." : string.Empty;
    }

    private async Task AddAsync()
    {
        var result = await _book.AddAsync(TopicChosen, _value.Text, _label.Text, ProfileOrigin.Manual, CancellationToken.None);
        _status.Text = ProfileMessages.For(result);
        if (result is ProfileEditResult.Done or ProfileEditResult.NotSaved)
        {
            _list.SelectedIndex = -1;
            _value.Text = string.Empty;
            _label.Text = string.Empty;
            await RefreshAsync();
            _status.Text = ProfileMessages.For(result);
        }
    }

    private async Task CorrectAsync()
    {
        if (Selected is not { } selected)
        {
            return;
        }

        var result = await _book.CorrectAsync(selected.Id, TopicChosen, _value.Text, _label.Text, CancellationToken.None);
        if (result is ProfileEditResult.Done or ProfileEditResult.NotSaved)
        {
            await RefreshAsync();
        }

        _status.Text = ProfileMessages.For(result);
    }

    private async Task RemoveAsync()
    {
        if (Selected is not { } selected)
        {
            return;
        }

        var result = await _book.RemoveAsync(selected.Id, CancellationToken.None);
        _list.SelectedIndex = -1;
        _value.Text = string.Empty;
        _label.Text = string.Empty;
        await RefreshAsync();
        _status.Text = result == ProfileEditResult.Done ? "Info supprimée." : ProfileMessages.For(result);
    }

    private async Task ExportAsync()
    {
        var values = await _book.GetValuesAsync(CancellationToken.None);
        if (values.Count == 0)
        {
            _status.Text = "Ta fiche est vide : rien à exporter.";
            return;
        }

        var path = _prompts.ChooseExportPath();
        var password = path is null ? null : _prompts.AskPassword(isNew: true);
        if (path is null || password is null)
        {
            return;
        }

        _status.Text = "Export en cours…";
        try
        {
            // Drawing the key from the password is slow on purpose: it runs aside, the window stays responsive
            var archive = await Task.Run(() => ProfileArchive.Export(values, password));
            await File.WriteAllBytesAsync(path, archive);
            _status.Text = $"Fiche exportée ({values.Count} infos). Garde bien le mot de passe : sans lui, l'export est illisible.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _status.Text = "Impossible d'écrire ce fichier : choisis un autre dossier.";
        }
        catch (ArgumentException)
        {
            _status.Text = $"Le mot de passe doit faire au moins {ProfileArchive.MinPasswordLength} caractères.";
        }
    }

    private async Task ImportAsync()
    {
        var path = _prompts.ChooseImportPath();
        var password = path is null ? null : _prompts.AskPassword(isNew: false);
        if (path is null || password is null)
        {
            return;
        }

        byte[] archive;
        try
        {
            if (new FileInfo(path).Length > MaxArchiveBytes)
            {
                _status.Text = ProfileMessages.For(ProfileArchiveStatus.NotAnArchive);
                return;
            }

            archive = await File.ReadAllBytesAsync(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _status.Text = "Impossible de lire ce fichier.";
            return;
        }

        _status.Text = "Lecture de l'export…";
        var reading = await Task.Run(() => ProfileArchive.Read(archive, password));
        if (reading.Status != ProfileArchiveStatus.Read)
        {
            _status.Text = ProfileMessages.For(reading.Status);
            return;
        }

        var summary = await _book.ImportAsync(reading.Values, CancellationToken.None);
        await RefreshAsync();
        _status.Text = ProfileMessages.For(summary);
    }
}
