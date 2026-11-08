using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PasteFlow.App.Design;
using PasteFlow.App.Highlights;
using PasteFlow.App.Notifications;
using PasteFlow.Core.Picking;

namespace PasteFlow.App.Picking;

/// <summary>
/// The list shown at the cursor when nothing is sure: the likely values, and a search in the windows as soon as the
/// user types.
/// </summary>
/// <remarks>
/// Keys: arrows and Entrée to choose, 1 to 5 before typing anything, Tab to widen the cut, Ctrl+Maj+arrows to adjust
/// the passage word by word, Échap to cancel. Clicking elsewhere cancels too.
/// </remarks>
internal sealed class PastePickerWindow : Window
{
    private const double MaxTextWidth = 460;
    private const double Gap = 6;

    private readonly PickerState _state;
    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TextBox _search;
    private readonly TextBlock _placeholder;
    private readonly StackPanel _cutLine;
    private readonly StackPanel _list;
    private readonly ContentControl _help;

    /// <summary>
    /// Initializes a new instance of the <see cref="PastePickerWindow"/> class, not shown yet.
    /// </summary>
    /// <param name="request">The values and the search to offer.</param>
    public PastePickerWindow(PickRequest request)
    {
        _state = new PickerState(request);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -32000;
        Top = -32000;
        MinWidth = 400;

        // A search field on top, as in Spotlight: a magnifier, then what the user types
        _search = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = PopupColors.Text,
            CaretBrush = Theme.Brush(palette => palette.Accent),
            BorderThickness = new Thickness(0),
            FontFamily = Theme.UiFont,
            FontSize = 15,
            Padding = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = PlainTextBoxTemplate(),
        };
        System.Windows.Automation.AutomationProperties.SetName(_search, "Chercher dans les fenêtres");
        _placeholder = Text("Chercher dans les fenêtres…", PopupColors.DiscreetText, 15);
        _placeholder.IsHitTestVisible = false;
        _cutLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 6, 8) };
        _list = new StackPanel();
        _help = new ContentControl { Focusable = false };

        var searchRow = new Grid { Margin = new Thickness(10, 4, 10, 10) };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var magnifier = Ui.Icon(Icons.Search, 15, PopupColors.DiscreetText);
        magnifier.Margin = new Thickness(0, 0, 10, 0);
        searchRow.Children.Add(magnifier);
        var searchBox = new Grid();
        searchBox.Children.Add(_placeholder);
        searchBox.Children.Add(_search);
        Grid.SetColumn(searchBox, 1);
        searchRow.Children.Add(searchBox);

        var panel = new StackPanel();
        panel.Children.Add(searchRow);
        panel.Children.Add(Ui.Divider(new Thickness(0, 0, 0, 8)));
        panel.Children.Add(_cutLine);
        panel.Children.Add(_list);
        panel.Children.Add(Ui.Divider(new Thickness(0, 8, 0, 8)));
        _help.Margin = new Thickness(8, 0, 8, 0);
        panel.Children.Add(_help);

        Content = Ui.PopupRoot(panel, new Thickness(6, 10, 6, 10));

        _search.TextChanged += (_, _) =>
        {
            _state.SetQuery(_search.Text);
            Render();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) =>
        {
            if (CancelsWhenDeactivated)
            {
                Finish(null);
            }
        };
        Render();
    }

    /// <summary>
    /// Gets the text chosen, or <see langword="null"/> once the user cancels.
    /// </summary>
    public Task<string?> Result => _result.Task;

    /// <summary>
    /// Gets or sets a value indicating whether clicking elsewhere cancels the choice; always, except in the tests.
    /// </summary>
    internal bool CancelsWhenDeactivated { get; set; } = true;

    /// <summary>
    /// Gets the state of the list, for the tests.
    /// </summary>
    internal PickerState State => _state;

    /// <summary>
    /// Shows the list below the cursor, or above it when there is no room below, and gives it the keyboard.
    /// </summary>
    /// <param name="caret">The place of the cursor in screen pixels.</param>
    public void ShowAt(Rect caret)
    {
        Show();
        UpdateLayout();

        var scale = VisualTreeHelper.GetDpi(this);
        var size = new Size(ActualWidth * scale.DpiScaleX, ActualHeight * scale.DpiScaleY);
        var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)caret.Left, (int)caret.Bottom)).WorkingArea;

        // The window holds room for the shadow around the card: the card itself lands just below the cursor
        var shadow = Ui.ShadowMargin * scale.DpiScaleX;
        var left = Math.Clamp(caret.Left - shadow, area.Left, Math.Max(area.Left, area.Right - size.Width));
        var top = caret.Bottom + Gap + size.Height - shadow <= area.Bottom
            ? caret.Bottom + Gap - shadow
            : Math.Max(area.Top, caret.Top - Gap - size.Height + shadow);
        OverlayWindows.Move(new WindowInteropHelper(this).Handle, new Point(left, top));

        _ = Activate();
        _ = _search.Focus();
    }

    /// <summary>
    /// Types letters in the search box, as the user would.
    /// </summary>
    /// <param name="text">The letters.</param>
    internal void TypeQuery(string text)
    {
        _search.Text = text;
    }

    /// <summary>
    /// Ends the choice: closes the list and gives the text chosen, or nothing.
    /// </summary>
    /// <param name="text">The text chosen, or <see langword="null"/> to cancel.</param>
    internal void Finish(string? text)
    {
        if (_result.TrySetResult(text))
        {
            Close();
        }
    }

    private static ControlTemplate PlainTextBoxTemplate()
    {
        // The search field has no frame of its own: the card is its frame
        var template = new ControlTemplate(typeof(TextBox));
        var host = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
        host.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        template.VisualTree = host;
        return template;
    }

    private static TextBlock Text(string text, Brush color, double size)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color,
            FontFamily = Theme.UiFont,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
    }

    private static int? DigitOf(Key key)
    {
        return key switch
        {
            >= Key.D1 and <= Key.D5 => key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad5 => key - Key.NumPad1,
            _ => null,
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var isAdjusting = Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift);
        switch (e.Key)
        {
            case Key.Escape:
                Finish(null);
                break;
            case Key.Enter:
                Finish(_state.Selected?.Text);
                break;
            case Key.Tab:
                _state.NextCut();
                break;
            case Key.Right when isAdjusting:
                _state.ExtendEnd();
                break;
            case Key.Left when isAdjusting:
                _state.ShrinkEnd();
                break;
            case Key.Up when isAdjusting:
                _state.ExtendStart();
                break;
            case Key.Down when isAdjusting:
                _state.ShrinkStart();
                break;
            case Key.Up:
                _state.MoveSelection(-1);
                break;
            case Key.Down:
                _state.MoveSelection(1);
                break;
            default:
                // Before anything is typed, a digit picks a value; afterwards, digits are searched like letters
                if (!_state.IsSearching && Keyboard.Modifiers == ModifierKeys.None && DigitOf(e.Key) is { } index && index < _state.Items.Count)
                {
                    Finish(_state.Items[index].Text);
                    break;
                }

                return;
        }

        e.Handled = true;
        Render();
    }

    private void Render()
    {
        _placeholder.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _cutLine.Visibility = _state.IsSearching ? Visibility.Visible : Visibility.Collapsed;
        _cutLine.Children.Clear();
        if (_state.IsSearching)
        {
            // The cut of the passages, as a small segmented choice that Tab moves along
            var label = Text("Découpe", PopupColors.DiscreetText, 11);
            label.Margin = new Thickness(0, 0, 8, 0);
            _cutLine.Children.Add(label);
            AddCut(PassageCut.Words, "mots");
            AddCut(PassageCut.Sentence, "phrase");
            AddCut(PassageCut.Line, "ligne");
            AddCut(PassageCut.List, "liste");
            AddCut(PassageCut.Page, "page");
            var tab = Ui.Keycap("Tab");
            tab.Margin = new Thickness(8, 0, 0, 0);
            _cutLine.Children.Add(tab);
        }

        _list.Children.Clear();
        if (_state.Items.Count == 0)
        {
            var empty = _state.IsSearching ? "Rien trouvé dans les fenêtres ouvertes." : "Aucune valeur reconnue : tape quelques lettres.";
            _list.Children.Add(new Border { Padding = new Thickness(12, 8, 12, 8), Child = Text(empty, PopupColors.SecondaryText, 13) });
        }

        for (var index = 0; index < _state.Items.Count; index++)
        {
            _list.Children.Add(Row(index, _state.Items[index], index == _state.SelectedIndex));
        }

        _help.Content = _state.IsSearching
            ? Ui.KeyHints(("↑↓", "choisir"), ("Entrée", "coller"), ("Ctrl+Maj+flèches", "mot par mot"), ("Échap", "annuler"))
            : Ui.KeyHints(("↑↓", "choisir"), ("1 à 5", "coller tout de suite"), ("Entrée", "coller"), ("Échap", "annuler"));
    }

    private void AddCut(PassageCut cut, string name)
    {
        var isCurrent = _state.Cut == cut;
        var chip = Text(name, isCurrent ? PopupColors.Text : PopupColors.DiscreetText, 11);
        chip.FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal;
        _cutLine.Children.Add(new Border
        {
            Background = isCurrent ? Theme.Brush(palette => palette.Sunken) : Brushes.Transparent,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(7, 1, 7, 2),
            Child = chip,
        });
    }

    private Border Row(int index, PickItem item, bool isSelected)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The value, with what it is and where it comes from below; its number on the right, as a key
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var value = Text(item.Text.ReplaceLineEndings(" "), PopupColors.Text, 14);
        value.MaxWidth = MaxTextWidth;
        texts.Children.Add(value);
        var origin = item.Description.Length > 0 ? $"{item.Description}, {item.SourceName}" : item.SourceName;
        var source = Text(origin, PopupColors.DiscreetText, 11);
        source.MaxWidth = MaxTextWidth;
        texts.Children.Add(source);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        var number = Ui.Keycap((index + 1).ToString(CultureInfo.InvariantCulture), isAccent: isSelected);
        number.Margin = new Thickness(14, 0, 0, 0);
        Grid.SetColumn(number, 2);
        if (!_state.IsSearching)
        {
            grid.Children.Add(number);
        }

        var row = new Border
        {
            Background = isSelected ? Theme.Brush(palette => palette.AccentSoft) : Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 7, 10, 7),
            Margin = new Thickness(4, 0, 4, 1),
            Cursor = Cursors.Hand,
            Child = grid,
        };
        row.MouseLeftButtonUp += (_, _) => Finish(item.Text);
        return row;
    }
}
