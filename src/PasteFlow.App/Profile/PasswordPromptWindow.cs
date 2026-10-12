using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Profile;

/// <summary>
/// Small window that asks the password of an export of the profile, twice for a new export.
/// </summary>
internal sealed class PasswordPromptWindow : Window
{
    private readonly bool _isNew;
    private readonly PasswordBox _password;
    private readonly PasswordBox _confirmation;
    private readonly TextBlock _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordPromptWindow"/> class.
    /// </summary>
    /// <param name="isNew"><see langword="true"/> for a new export; <see langword="false"/> to open an existing one.</param>
    public PasswordPromptWindow(bool isNew)
    {
        _isNew = isNew;
        Title = isNew ? "Protéger l'export de ta fiche" : "Ouvrir un export de ta fiche";
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _password = new PasswordBox { Margin = new Thickness(0, 4, 0, 8), MinWidth = 280 };
        AutomationProperties.SetName(_password, "Mot de passe");
        _confirmation = new PasswordBox { Margin = new Thickness(0, 4, 0, 8), MinWidth = 280 };
        AutomationProperties.SetName(_confirmation, "Confirmer le mot de passe");
        _error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

        var ok = new Button { Content = "Valider", IsDefault = true, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => Validate();
        var cancel = new Button { Content = "Annuler", IsCancel = true, MinWidth = 100 };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(20), MaxWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            Text = isNew
                ? $"Choisis un mot de passe d'au moins {ProfileArchive.MinPasswordLength} caractères. Il sera demandé pour importer ta fiche sur un autre PC : sans lui, l'export est illisible."
                : "Tape le mot de passe choisi au moment de l'export.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(new TextBlock { Text = "Mot de passe" });
        panel.Children.Add(_password);
        if (isNew)
        {
            panel.Children.Add(new TextBlock { Text = "Confirmer le mot de passe" });
            panel.Children.Add(_confirmation);
        }

        panel.Children.Add(_error);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => _ = _password.Focus();
    }

    /// <summary>
    /// Gets the password typed, once the window is validated.
    /// </summary>
    internal string? Password { get; private set; }

    private void Validate()
    {
        var password = _password.Password;
        if (_isNew && password.Length < ProfileArchive.MinPasswordLength)
        {
            _error.Text = $"Au moins {ProfileArchive.MinPasswordLength} caractères.";
            return;
        }

        if (_isNew && !string.Equals(password, _confirmation.Password, StringComparison.Ordinal))
        {
            _error.Text = "Les deux mots de passe ne sont pas identiques.";
            return;
        }

        Password = password;
        DialogResult = true;
    }
}
