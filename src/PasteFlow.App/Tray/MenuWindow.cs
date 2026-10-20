using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PasteFlow.App.Design;

namespace PasteFlow.App.Tray;

/// <summary>
/// The menu of PasteFlow opened with its own shortcut, so that everything the icon of the taskbar offers can be done
/// without the mouse: one letter runs an entry, the arrows and Entrée too, Échap closes.
/// </summary>
internal sealed class MenuWindow : Window
{
    private readonly IReadOnlyList<MenuEntry> _entries;
    private readonly List<Button> _buttons = new();
    private bool _isClosing;
    private bool _isNearTray;

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuWindow"/> class.
    /// </summary>
    /// <param name="entries">The entries of the menu, in order.</param>
    public MenuWindow(IReadOnlyList<MenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries;
        Title = "Menu PasteFlow";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;

        // A small command palette: the name of the app, the commands with their letter, the keys to use it
        var header = new DockPanel { Margin = new Thickness(10, 2, 10, 10) };
        var hint = Ui.Line("Tape une lettre", TextRole.Hint);
        hint.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(hint, Dock.Right);
        header.Children.Add(hint);
        header.Children.Add(Ui.Line("PasteFlow", TextRole.Heading));

        var list = new StackPanel { MinWidth = 380 };
        foreach (var entry in entries)
        {
            var button = CreateButton(entry);
            _buttons.Add(button);
            list.Children.Add(button);
        }

        KeyboardNavigation.SetDirectionalNavigation(list, KeyboardNavigationMode.Cycle);
        var panel = new StackPanel();
        panel.Children.Add(header);
        panel.Children.Add(Ui.Divider(new Thickness(0, 0, 0, 6)));
        panel.Children.Add(list);
        panel.Children.Add(Ui.Divider(new Thickness(0, 6, 0, 8)));
        var keys = Ui.KeyHints(("↑↓", "choisir"), ("Entrée", "ouvrir"), ("Échap", "fermer"));
        keys.Margin = new Thickness(10, 0, 10, 0);
        panel.Children.Add(keys);
        Content = Ui.PopupRoot(panel, new Thickness(6, 12, 6, 10));
        PreviewKeyDown += OnPreviewKeyDown;

        // Clicking elsewhere closes the menu, once the activation is over; a window already closing is left alone
        Closing += (_, _) => _isClosing = true;
        Deactivated += (_, _) => _ = Dispatcher.BeginInvoke(new Action(CloseOnce));
        Loaded += (_, _) =>
        {
            if (_isNearTray)
            {
                PlaceNearTray();
            }

            _ = Activate();
            _ = _buttons.FirstOrDefault(button => button.IsEnabled)?.Focus();
        };
    }

    /// <summary>
    /// Gets the texts of the entries, as the user sees them.
    /// </summary>
    internal IReadOnlyList<string> Lines => _entries.Select(Line).ToList();

    /// <summary>
    /// Shows the menu in the corner of the screen, above the icon of the notification area, as when it was clicked.
    /// </summary>
    internal void ShowNearTray()
    {
        _isNearTray = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Show();
    }

    /// <summary>
    /// Gets the line of an entry: its letter, then what it does.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The line, such as "F   Ma fiche…".</returns>
    internal static string Line(MenuEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return $"{entry.Letter}   {entry.Text}";
    }

    /// <summary>
    /// Runs the entry of a letter, as when the user types it.
    /// </summary>
    /// <param name="letter">The letter typed.</param>
    /// <returns><see langword="true"/> if an entry was run; otherwise <see langword="false"/>.</returns>
    internal bool RunLetter(char letter)
    {
        var entry = _entries.FirstOrDefault(candidate => char.ToUpperInvariant(candidate.Letter) == char.ToUpperInvariant(letter));
        if (entry is not { IsEnabled: true })
        {
            return false;
        }

        Run(entry);
        return true;
    }

    private static char? LetterOf(Key key)
    {
        return key is >= Key.A and <= Key.Z ? (char)('A' + (key - Key.A)) : null;
    }

    private Button CreateButton(MenuEntry entry)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (entry.Icon.Length > 0)
        {
            var icon = Ui.Icon(entry.Icon, 14);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            row.Children.Add(icon);
        }

        var text = Ui.Line(entry.Text, TextRole.Body);
        text.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        var key = Ui.Keycap(entry.Letter.ToString());
        key.Margin = new Thickness(16, 0, 0, 0);
        Grid.SetColumn(key, 2);
        row.Children.Add(key);

        var button = new Button { Content = row, IsEnabled = entry.IsEnabled };
        button.SetResourceReference(StyleProperty, "Pf.RowButton");
        AutomationProperties.SetName(button, entry.Text);
        AutomationProperties.SetAccessKey(button, entry.Letter.ToString());
        button.Click += (_, _) => Run(entry);
        return button;
    }

    private void PlaceNearTray()
    {
        // The room of the shadow sits outside the card: the card itself lands a little away from the taskbar
        const double Gap = 10;
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth + Ui.ShadowMargin - Gap;
        Top = area.Bottom - ActualHeight + Ui.ShadowMargin - Gap;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseOnce();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && LetterOf(e.Key) is { } letter)
        {
            e.Handled = RunLetter(letter);
        }
    }

    private void Run(MenuEntry entry)
    {
        // The menu closes first, so that the window the entry opens gets the keyboard
        CloseOnce();
        entry.Run();
    }

    private void CloseOnce()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        Close();
    }
}
