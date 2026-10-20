using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PasteFlow.App.Design;
using PasteFlow.Core.Logins;

namespace PasteFlow.App.Logins;

/// <summary>
/// Small window where the user chooses the account to sign in with.
/// </summary>
internal sealed class LoginPickerWindow : ThemedWindow
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
        Topmost = true;

        _list = new ListBox { Width = 380, MaxHeight = 280 };
        AutomationProperties.SetName(_list, "Comptes");
        foreach (var login in logins)
        {
            // Two lines per account: its name, then its identifier
            var line = Line(login);
            var texts = new StackPanel();
            texts.Children.Add(Ui.Line(login.Name, TextRole.Body));
            if (login.UserName.Length > 0)
            {
                texts.Children.Add(Ui.Line(login.UserName, TextRole.Caption));
            }

            var row = new DockPanel();
            var key = Ui.Icon(Icons.Key, 14);
            key.Margin = new Thickness(2, 0, 12, 0);
            DockPanel.SetDock(key, Dock.Left);
            row.Children.Add(key);
            row.Children.Add(texts);
            var item = new ListBoxItem { Content = row };
            AutomationProperties.SetName(item, line);
            _ = _list.Items.Add(item);
        }

        _list.SelectedIndex = 0;
        _list.MouseDoubleClick += (_, _) => Choose();

        var choose = Ui.ActionButton("Remplir", isDefault: true);
        choose.Click += (_, _) => Choose();
        var cancel = Ui.ActionButton("Annuler", isCancel: true);

        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 22) };
        panel.Children.Add(Ui.Header(Icons.Key, "Quel compte ?", "Windows Hello te sera demandé avant le mot de passe."));
        panel.Children.Add(_list);
        panel.Children.Add(Ui.ButtonRow(cancel, choose));
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
