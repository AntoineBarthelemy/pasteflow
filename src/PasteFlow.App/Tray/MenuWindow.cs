using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

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
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;

        var panel = new StackPanel { Margin = new Thickness(12), MinWidth = 320 };
        panel.Children.Add(new TextBlock
        {
            Text = "Tape une lettre, ou les flèches puis Entrée. Échap ferme.",
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360,
        });
        foreach (var entry in entries)
        {
            var button = CreateButton(entry);
            _buttons.Add(button);
            panel.Children.Add(button);
        }

        Content = panel;
        KeyboardNavigation.SetDirectionalNavigation(panel, KeyboardNavigationMode.Cycle);
        PreviewKeyDown += OnPreviewKeyDown;
        Deactivated += (_, _) => CloseOnce();
        Loaded += (_, _) =>
        {
            _ = Activate();
            _ = _buttons.FirstOrDefault(button => button.IsEnabled)?.Focus();
        };
    }

    /// <summary>
    /// Gets the texts of the entries, as the user sees them.
    /// </summary>
    internal IReadOnlyList<string> Lines => _entries.Select(Line).ToList();

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
        var button = new Button
        {
            Content = Line(entry),
            IsEnabled = entry.IsEnabled,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 2, 0, 2),
        };
        AutomationProperties.SetName(button, entry.Text);
        AutomationProperties.SetAccessKey(button, entry.Letter.ToString());
        button.Click += (_, _) => Run(entry);
        return button;
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
