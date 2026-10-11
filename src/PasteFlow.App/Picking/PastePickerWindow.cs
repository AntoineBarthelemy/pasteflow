using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

    private static readonly Brush SelectedBackground = Frozen(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly Brush BadgeBackground = Frozen(Color.FromRgb(0x4A, 0x4A, 0x4A));
    private static readonly Brush SearchBackground = Frozen(Color.FromRgb(0x2E, 0x2E, 0x2E));

    private readonly PickerState _state;
    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TextBox _search;
    private readonly TextBlock _placeholder;
    private readonly TextBlock _cutLine;
    private readonly StackPanel _list;
    private readonly TextBlock _help;

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
        MinWidth = 360;

        _search = new TextBox
        {
            Background = Brushes.Transparent,
            Foreground = PopupColors.Text,
            CaretBrush = PopupColors.Text,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        System.Windows.Automation.AutomationProperties.SetName(_search, "Chercher dans les fenêtres");
        _placeholder = Text("Tape pour chercher dans les fenêtres", PopupColors.DiscreetText, 14);
        _placeholder.IsHitTestVisible = false;
        _cutLine = Text(string.Empty, PopupColors.DiscreetText, 12);
        _list = new StackPanel();
        _help = Text(string.Empty, PopupColors.DiscreetText, 11);

        var searchBox = new Grid { Margin = new Thickness(2, 0, 2, 6) };
        searchBox.Children.Add(_placeholder);
        searchBox.Children.Add(_search);
        var panel = new StackPanel();
        panel.Children.Add(new Border
        {
            Background = SearchBackground,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 6),
            Child = searchBox,
        });
        panel.Children.Add(_cutLine);
        panel.Children.Add(_list);
        panel.Children.Add(new Border
        {
            BorderBrush = PopupColors.Separator,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(4, 6, 4, 0),
            Child = _help,
        });

        Content = new Border
        {
            Background = PopupColors.Background,
            BorderBrush = PopupColors.ToCheck,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Child = panel,
        };

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
        var left = Math.Clamp(caret.Left, area.Left, Math.Max(area.Left, area.Right - size.Width));
        var top = caret.Bottom + Gap + size.Height <= area.Bottom ? caret.Bottom + Gap : Math.Max(area.Top, caret.Top - Gap - size.Height);
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

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static TextBlock Text(string text, Brush color, double size)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = color,
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
        _cutLine.Inlines.Clear();
        if (_state.IsSearching)
        {
            _cutLine.Inlines.Add(new Run("Découpe : "));
            AddCut(PassageCut.Words, "mots");
            _cutLine.Inlines.Add(new Run(" · "));
            AddCut(PassageCut.Sentence, "phrase");
            _cutLine.Inlines.Add(new Run(" · "));
            AddCut(PassageCut.Line, "ligne");
            _cutLine.Inlines.Add(new Run("   (Tab)"));
        }

        _list.Children.Clear();
        if (_state.Items.Count == 0)
        {
            var empty = _state.IsSearching ? "Rien trouvé dans les fenêtres ouvertes." : "Aucune valeur reconnue : tape quelques lettres.";
            _list.Children.Add(new Border { Padding = new Thickness(8, 6, 8, 6), Child = Text(empty, PopupColors.SecondaryText, 13) });
        }

        for (var index = 0; index < _state.Items.Count; index++)
        {
            _list.Children.Add(Row(index, _state.Items[index], index == _state.SelectedIndex));
        }

        _help.Text = _state.IsSearching
            ? "↑ ↓ choisir · Entrée coller · Ctrl+Maj+flèches : mot par mot · Échap"
            : "↑ ↓ ou 1 à 5 · Entrée coller · Échap annule";
    }

    private void AddCut(PassageCut cut, string name)
    {
        var run = new Run(name);
        if (_state.Cut == cut)
        {
            run.Foreground = PopupColors.ToCheck;
            run.FontWeight = FontWeights.SemiBold;
        }

        _cutLine.Inlines.Add(run);
    }

    private Border Row(int index, PickItem item, bool isSelected)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = new Border
        {
            Background = isSelected ? PopupColors.ToCheck : BadgeBackground,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 0, 5, 0),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text((index + 1).ToString(CultureInfo.InvariantCulture), isSelected ? PopupColors.Background : PopupColors.Text, 11),
        };
        var value = Text(item.Text.ReplaceLineEndings(" "), PopupColors.Text, 14);
        value.MaxWidth = MaxTextWidth;
        var origin = item.Description.Length > 0 ? $"{item.Description} · {item.SourceName}" : item.SourceName;
        var source = Text(origin, PopupColors.DiscreetText, 11);
        source.Margin = new Thickness(12, 0, 0, 0);
        source.MaxWidth = 180;
        Grid.SetColumn(value, 1);
        Grid.SetColumn(source, 2);
        grid.Children.Add(badge);
        grid.Children.Add(value);
        grid.Children.Add(source);

        var row = new Border
        {
            Background = isSelected ? SelectedBackground : Brushes.Transparent,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 6, 8, 6),
            Cursor = Cursors.Hand,
            Child = grid,
        };
        row.MouseLeftButtonUp += (_, _) => Finish(item.Text);
        return row;
    }
}
