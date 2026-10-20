using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using PasteFlow.App.Design;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Security;

namespace PasteFlow.App.Profile;

/// <summary>
/// Window where the user sees their profile, adds, corrects and removes informations, and exports or imports it.
/// </summary>
/// <remarks>
/// Sensitive values are never shown whole without Windows Hello: only their last characters. Exporting a profile with
/// sensitive values also asks for Windows Hello.
/// </remarks>
internal sealed class ProfileWindow : ThemedWindow
{
    // An export holds a few hundred values at most: anything far bigger is not one
    private const int MaxArchiveBytes = 1024 * 1024;

    private const double ContentWidth = 520;

    private readonly ProfileBook _book;
    private readonly SensitiveGate _gate;
    private readonly IProfileTransferPrompts _prompts;
    private readonly ListBox _list;
    private readonly ComboBox _topic;
    private readonly TextBox _value;
    private readonly TextBox _label;
    private readonly Button _correct;
    private readonly Button _remove;
    private readonly Button _show;
    private readonly TextBlock _status;
    private readonly TextBlock _empty;
    private IReadOnlyList<ProfileValue> _values = Array.Empty<ProfileValue>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileWindow"/> class.
    /// </summary>
    /// <param name="book">The profile shown and changed.</param>
    /// <param name="gate">The gate that guards the sensitive values.</param>
    /// <param name="createPrompts">Creates, for this window, what asks where to export or import and the password.</param>
    public ProfileWindow(ProfileBook book, SensitiveGate gate, Func<Window, IProfileTransferPrompts> createPrompts)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(createPrompts);

        _book = book;
        _gate = gate;
        _prompts = createPrompts(this);
        Title = "Ma fiche PasteFlow";

        _list = new ListBox { Height = 250 };
        AutomationProperties.SetName(_list, "Infos de ma fiche");
        _list.SelectionChanged += (_, _) => ShowSelected();
        _empty = Ui.Text("Ta fiche est vide. Ajoute ta première info juste en dessous.", TextRole.Secondary);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _empty.IsHitTestVisible = false;

        _topic = new ComboBox { ItemsSource = ProfileTopics.All.Select(ProfileTopics.DisplayName).ToList(), SelectedIndex = 0 };
        AutomationProperties.SetName(_topic, "Sujet");
        _value = new TextBox();
        AutomationProperties.SetName(_value, "Valeur");
        _label = new TextBox();
        AutomationProperties.SetName(_label, "Nom (facultatif)");

        var add = Ui.ActionButton("_Ajouter");
        add.SetResourceReference(StyleProperty, "Pf.PrimaryButton");
        add.Click += async (_, _) => await AddAsync();
        _correct = Ui.ActionButton("_Corriger");
        _correct.Click += async (_, _) => await CorrectAsync();
        _remove = Ui.ActionButton("Su_pprimer");
        _remove.Click += async (_, _) => await RemoveAsync();
        _show = Ui.ActionButton("A_fficher");
        _show.Click += async (_, _) => await ShowSensitiveAsync();
        var export = Ui.QuietButton("_Exporter…");
        export.Click += async (_, _) => await ExportAsync();
        var import = Ui.QuietButton("_Importer…");
        import.Click += async (_, _) => await ImportAsync();
        var close = Ui.ActionButton("Fermer", isCancel: true);
        close.Click += (_, _) => Close();

        _status = Ui.Text(string.Empty, TextRole.Caption);
        _status.MinHeight = 34;
        _status.Margin = new Thickness(2, 10, 0, 0);
        AutomationProperties.SetName(_status, "État");

        var page = new StackPanel { Margin = new Thickness(28, 24, 28, 22), Width = ContentWidth };
        page.Children.Add(Ui.Header(
            Icons.Person,
            "Ma fiche",
            "Les infos que PasteFlow peut écrire pour toi, chiffrées sur ce PC. Les infos sensibles ne s'affichent en entier qu'avec Windows Hello."));

        // The list, with a sentence in its middle while it is empty
        var listTitle = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var deleteHint = Ui.KeyHints(("Suppr", "retire l'info choisie"));
        deleteHint.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(deleteHint, Dock.Right);
        listTitle.Children.Add(deleteHint);
        listTitle.Children.Add(new Label { Content = "_Mes infos", Target = _list, Margin = new Thickness(0) });
        page.Children.Add(listTitle);
        page.Children.Add(new Grid { Children = { _list, _empty } });

        // The form that adds an info, or corrects the one chosen
        var form = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var topicField = Ui.Field("_Sujet", _topic);
        var labelField = Ui.Field("_Nom (facultatif)", _label);
        Grid.SetColumn(labelField, 2);
        var valueField = Ui.Field("_Valeur", _value);
        Grid.SetRow(valueField, 1);
        Grid.SetColumnSpan(valueField, 3);
        form.Children.Add(topicField);
        form.Children.Add(labelField);
        form.Children.Add(valueField);
        page.Children.Add(form);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var button in new[] { add, _correct, _remove, _show })
        {
            button.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(button);
        }

        page.Children.Add(actions);
        page.Children.Add(_status);

        var footer = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        close.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(close);
        var transfer = new StackPanel { Orientation = Orientation.Horizontal };
        transfer.Children.Add(export);
        transfer.Children.Add(import);
        footer.Children.Add(transfer);
        page.Children.Add(Ui.Divider(new Thickness(0, 4, 0, 14)));
        page.Children.Add(footer);
        Content = page;

        // With the keyboard: Entrée in the value or the name adds it, or corrects the info chosen; Suppr in the list
        // removes the info chosen
        _value.KeyDown += async (_, e) => await OnEnterAsync(e);
        _label.KeyDown += async (_, e) => await OnEnterAsync(e);
        _list.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Delete && Selected is not null)
            {
                e.Handled = true;
                await RemoveAsync();
            }
        };

        ShowSelected();
        Loaded += async (_, _) =>
        {
            await RefreshAsync();
            _ = _values.Count > 0 ? _list.Focus() : _value.Focus();
        };
    }

    /// <summary>
    /// Gets the lines of the list, as the user sees them.
    /// </summary>
    internal IReadOnlyList<string> Lines => _list.Items.Cast<ListBoxItem>().Select(item => (string)item.Tag).ToList();

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
        var origin = OriginName(value.Origin);
        return origin.Length == 0 ? $"{value.ShownLabel} : {value.ShownValue}" : $"{value.ShownLabel} : {value.ShownValue} ({origin})";
    }

    /// <summary>
    /// Chooses the topic of the value typed, as the user does with the list of topics.
    /// </summary>
    /// <param name="topic">The topic.</param>
    internal void ChooseTopic(ProfileTopic topic)
    {
        _topic.SelectedIndex = ProfileTopics.All.ToList().IndexOf(topic);
    }

    private static DockPanel Row(ProfileValue value)
    {
        // The name small above, the value below; where it comes from and whether it is sensitive on the right
        var row = new DockPanel();
        var marks = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var origin = OriginName(value.Origin);
        if (origin.Length > 0)
        {
            marks.Children.Add(new Border
            {
                Background = Theme.Brush(palette => palette.Sunken),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 1, 7, 2),
                Margin = new Thickness(8, 0, 0, 0),
                Child = Ui.Line(origin, TextRole.Hint),
            });
        }

        if (value.IsSensitive)
        {
            var shield = Ui.Icon(Icons.Shield, 13);
            shield.Margin = new Thickness(8, 0, 2, 0);
            shield.ToolTip = "Info sensible : Windows Hello pour l'afficher";
            marks.Children.Add(shield);
        }

        DockPanel.SetDock(marks, Dock.Right);
        row.Children.Add(marks);
        var texts = new StackPanel();
        texts.Children.Add(Ui.Line(value.ShownLabel, TextRole.Hint));
        var shown = Ui.Line(value.ShownValue, TextRole.Body);
        shown.Margin = new Thickness(0, 1, 0, 0);
        if (value.IsSensitive)
        {
            shown.FontFamily = Theme.MonoFont;
        }

        texts.Children.Add(shown);
        row.Children.Add(texts);
        return row;
    }

    private static string OriginName(ProfileOrigin origin)
    {
        return origin switch
        {
            ProfileOrigin.Learned => "appris",
            ProfileOrigin.WindowsAccount => "compte Windows",
            ProfileOrigin.PasswordManager => "Bitwarden",
            _ => string.Empty,
        };
    }

    private async Task RefreshAsync()
    {
        var selectedId = Selected?.Id;
        _values = await _book.GetValuesAsync(CancellationToken.None);
        _list.Items.Clear();
        foreach (var value in _values)
        {
            var line = Line(value);
            var item = new ListBoxItem { Content = Row(value), Tag = line };
            AutomationProperties.SetName(item, line);
            _ = _list.Items.Add(item);
        }

        _empty.Visibility = _values.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var index = _values.ToList().FindIndex(value => value.Id == selectedId);
        _list.SelectedIndex = index;
        ShowSelected();
    }

    private void ShowSelected()
    {
        var selected = Selected;
        _correct.IsEnabled = selected is not null;
        _remove.IsEnabled = selected is not null;
        _show.IsEnabled = selected is { IsSensitive: true };
        if (selected is null)
        {
            return;
        }

        _topic.SelectedIndex = ProfileTopics.All.ToList().IndexOf(selected.Topic);
        _label.Text = selected.Label;

        // A sensitive value is never put back on screen: the user retypes it to correct it
        _value.Text = selected.IsSensitive ? string.Empty : selected.Value;
        _status.Text = selected.IsSensitive ? "Valeur sensible masquée : « Afficher » la montre avec Windows Hello, ou retape-la en entier pour la corriger." : string.Empty;
    }

    private async Task OnEnterAsync(KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (Selected is null)
        {
            await AddAsync();
        }
        else
        {
            await CorrectAsync();
        }
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

    private async Task ShowSensitiveAsync()
    {
        if (Selected is not { IsSensitive: true } selected)
        {
            return;
        }

        var presence = await _gate.OpenAsync("Afficher une info sensible de ta fiche PasteFlow", CancellationToken.None);
        if (presence != PresenceResult.Verified)
        {
            _status.Text = ProfileMessages.For(presence);
            return;
        }

        // Still the same value selected: the user may have chosen another one while Windows Hello was open
        if (Selected?.Id == selected.Id)
        {
            _value.Text = selected.Value;
            _status.Text = "Valeur affichée. Corrige-la si besoin, puis « Corriger ».";
        }
    }

    private async Task ExportAsync()
    {
        var values = await _book.GetValuesAsync(CancellationToken.None);
        if (values.Count == 0)
        {
            _status.Text = "Ta fiche est vide : rien à exporter.";
            return;
        }

        if (values.Any(value => value.IsSensitive))
        {
            var presence = await _gate.OpenAsync("Exporter ta fiche PasteFlow, avec ses infos sensibles", CancellationToken.None);
            if (presence != PresenceResult.Verified)
            {
                _status.Text = ProfileMessages.For(presence);
                return;
            }
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
