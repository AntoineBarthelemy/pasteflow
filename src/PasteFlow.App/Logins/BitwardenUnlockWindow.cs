using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PasteFlow.App.Design;

namespace PasteFlow.App.Logins;

/// <summary>
/// Small window that asks the master password of Bitwarden, to let PasteFlow fill the sign-in forms.
/// </summary>
/// <remarks>The master password is handed to Bitwarden at once and never kept by PasteFlow.</remarks>
internal sealed class BitwardenUnlockWindow : ThemedWindow
{
    private readonly Func<string, Task<bool>> _unlock;
    private readonly PasswordBox _password;
    private readonly TextBox _shownPassword;
    private readonly ToggleButton _eye;
    private readonly TextBlock _error;
    private readonly Button _unlockButton;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenUnlockWindow"/> class.
    /// </summary>
    /// <param name="unlock">Unlocks Bitwarden with the master password typed; tells whether it worked.</param>
    public BitwardenUnlockWindow(Func<string, Task<bool>> unlock)
    {
        ArgumentNullException.ThrowIfNull(unlock);

        _unlock = unlock;
        Title = "Déverrouiller Bitwarden";

        _password = new PasswordBox { VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_password, "Mot de passe maître");

        // The same password, shown in clear while the eye is pressed, so that a typing mistake can be seen
        _shownPassword = new TextBox { Visibility = Visibility.Collapsed, VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(_shownPassword, "Mot de passe maître affiché");
        _eye = new ToggleButton
        {
            Content = Icons.Eye,
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Afficher le mot de passe",
        };
        AutomationProperties.SetName(_eye, "Afficher le mot de passe");
        _eye.Checked += (_, _) => ShowPassword(true);
        _eye.Unchecked += (_, _) => ShowPassword(false);

        // The hidden and the shown password take the same place, the eye sits on their right
        var passwordRow = new Grid { Width = 380 };
        passwordRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        passwordRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_eye, 1);
        passwordRow.Children.Add(_password);
        passwordRow.Children.Add(_shownPassword);
        passwordRow.Children.Add(_eye);
        _error = Ui.Text(string.Empty, TextRole.Caption);
        _error.Margin = new Thickness(0, 8, 0, 0);
        AutomationProperties.SetName(_error, "Erreur");

        _unlockButton = Ui.ActionButton("Déverrouiller", isDefault: true);
        _unlockButton.Click += async (_, _) => await UnlockAsync();
        var later = Ui.ActionButton("Plus tard", isCancel: true);

        var label = new Label { Content = "_Mot de passe maître", Target = _password };
        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 22), Width = 380 };
        panel.Children.Add(Ui.Header(
            Icons.Lock,
            "Bitwarden",
            "Pour remplir tes pages de connexion. Ton mot de passe maître va directement à Bitwarden, PasteFlow ne le garde pas. Windows Hello te sera demandé avant chaque mot de passe, une fois par demi-heure."));
        panel.Children.Add(label);
        panel.Children.Add(passwordRow);
        panel.Children.Add(_error);
        panel.Children.Add(Ui.ButtonRow(later, _unlockButton));
        Content = panel;
        Loaded += (_, _) => _ = _password.Focus();
    }

    /// <summary>
    /// Gets a value indicating whether Bitwarden was unlocked from this window.
    /// </summary>
    internal bool IsUnlocked { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the master password is shown in clear.
    /// </summary>
    internal bool IsPasswordShown => _eye.IsChecked == true;

    /// <summary>
    /// Gets the master password typed, shown or hidden.
    /// </summary>
    internal string TypedPassword => IsPasswordShown ? _shownPassword.Text : _password.Password;

    private async Task UnlockAsync()
    {
        _unlockButton.IsEnabled = false;
        _error.Foreground = Theme.Brush(palette => palette.TextSecondary);
        _error.Text = "Déverrouillage…";
        var password = TypedPassword;
        _password.Clear();
        _shownPassword.Clear();
        IsUnlocked = await _unlock(password);
        if (IsUnlocked)
        {
            Close();
            return;
        }

        _error.Foreground = Theme.Brush(palette => palette.Danger);
        _error.Text = "Mot de passe maître incorrect, ou Bitwarden ne répond pas.";
        _unlockButton.IsEnabled = true;
        _ = (IsPasswordShown ? (Control)_shownPassword : _password).Focus();
    }

    private void ShowPassword(bool isShown)
    {
        if (isShown)
        {
            _shownPassword.Text = _password.Password;
            _password.Clear();
        }
        else
        {
            _password.Password = _shownPassword.Text;
            _shownPassword.Clear();
        }

        _shownPassword.Visibility = isShown ? Visibility.Visible : Visibility.Collapsed;
        _password.Visibility = isShown ? Visibility.Collapsed : Visibility.Visible;
        _eye.ToolTip = isShown ? "Masquer le mot de passe" : "Afficher le mot de passe";
        _eye.Content = isShown ? Icons.EyeHidden : Icons.Eye;
        var box = isShown ? (Control)_shownPassword : _password;
        _ = box.Focus();
        if (isShown)
        {
            _shownPassword.CaretIndex = _shownPassword.Text.Length;
        }
    }
}
