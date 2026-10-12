using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace PasteFlow.App.Logins;

/// <summary>
/// Small window that asks the master password of Bitwarden, to let PasteFlow fill the sign-in forms.
/// </summary>
/// <remarks>The master password is handed to Bitwarden at once and never kept by PasteFlow.</remarks>
internal sealed class BitwardenUnlockWindow : Window
{
    private readonly Func<string, Task<bool>> _unlock;
    private readonly PasswordBox _password;
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
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        _password = new PasswordBox { Margin = new Thickness(0, 4, 0, 8), MinWidth = 280 };
        AutomationProperties.SetName(_password, "Mot de passe maître");
        _error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(_error, "Erreur");

        _unlockButton = new Button { Content = "Déverrouiller", IsDefault = true, MinWidth = 110, Margin = new Thickness(0, 0, 8, 0) };
        _unlockButton.Click += async (_, _) => await UnlockAsync();
        var later = new Button { Content = "Plus tard", IsCancel = true, MinWidth = 110 };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(_unlockButton);
        buttons.Children.Add(later);

        var panel = new StackPanel { Margin = new Thickness(20), MaxWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            Text = "Pour remplir tes pages de connexion, PasteFlow demande tes comptes à Bitwarden. "
                + "Ton mot de passe maître va directement à Bitwarden : PasteFlow ne le garde pas. "
                + "Ensuite, Windows Hello te sera demandé avant chaque mot de passe (une fois par demi-heure).",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(new TextBlock { Text = "Mot de passe maître" });
        panel.Children.Add(_password);
        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _ = _password.Focus();
    }

    /// <summary>
    /// Gets a value indicating whether Bitwarden was unlocked from this window.
    /// </summary>
    internal bool IsUnlocked { get; private set; }

    private async Task UnlockAsync()
    {
        _unlockButton.IsEnabled = false;
        _error.Text = "Déverrouillage…";
        var password = _password.Password;
        _password.Clear();
        IsUnlocked = await _unlock(password);
        if (IsUnlocked)
        {
            Close();
            return;
        }

        _error.Text = "Mot de passe maître incorrect, ou Bitwarden ne répond pas.";
        _unlockButton.IsEnabled = true;
        _ = _password.Focus();
    }
}
