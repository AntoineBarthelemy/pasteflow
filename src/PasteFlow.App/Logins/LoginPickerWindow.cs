using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PasteFlow.Core.Logins;

namespace PasteFlow.App.Logins;

/// <summary>
/// Small window where the user chooses the account to sign in with.
/// </summary>
internal sealed class LoginPickerWindow : Window
{
    private readonly IReadOnlyList<VaultLogin> _logins;
    private readonly ListBox _list;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginPickerWindow"/> class.
    /// </summary>
    /// <param name="logins">The accounts to choose from.</param>
    public LoginPickerWindow(IReadOnlyList<VaultLogin> logins)
    {
        ArgumentNullException.ThrowIfNull(logins);

        _logins = logins;
        Title = "Quel compte Bitwarden ?";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        _list = new ListBox { MinWidth = 320, MaxHeight = 260, Margin = new Thickness(0, 8, 0, 8), SelectedIndex = 0 };
        AutomationProperties.SetName(_list, "Comptes");
        foreach (var login in logins)
        {
            var line = Line(login);
            var item = new ListBoxItem { Content = line };
            AutomationProperties.SetName(item, line);
            _ = _list.Items.Add(item);
        }

        _list.SelectedIndex = 0;
        _list.MouseDoubleClick += (_, _) => Choose();

        var choose = new Button { Content = "Remplir", IsDefault = true, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
        choose.Click += (_, _) => Choose();
        var cancel = new Button { Content = "Annuler", IsCancel = true, MinWidth = 100 };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(choose);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Choisis le compte à utiliser. Windows Hello te sera demandé avant le mot de passe.", TextWrapping = TextWrapping.Wrap, MaxWidth = 360 });
        panel.Children.Add(_list);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _ = _list.Focus();
    }

    /// <summary>
    /// Gets the account chosen, once the window is validated.
    /// </summary>
    internal VaultLogin? Chosen { get; private set; }

    /// <summary>
    /// Gets the line of an account in the list: its name and its identifier.
    /// </summary>
    /// <param name="login">The account.</param>
    /// <returns>The line.</returns>
    internal static string Line(VaultLogin login)
    {
        ArgumentNullException.ThrowIfNull(login);

        return login.UserName.Length > 0 ? $"{login.Name} ({login.UserName})" : login.Name;
    }

    private void Choose()
    {
        if (_list.SelectedIndex < 0)
        {
            return;
        }

        Chosen = _logins[_list.SelectedIndex];
        DialogResult = true;
    }
}
