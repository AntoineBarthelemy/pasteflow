using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PasteFlow.App.Design;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Profile;

/// <summary>
/// Small window that asks the password of an export of the profile, twice for a new export.
/// </summary>
internal sealed class PasswordPromptWindow : ThemedWindow
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
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _password = new PasswordBox { Width = 340 };
        AutomationProperties.SetName(_password, "Mot de passe");
        _confirmation = new PasswordBox { Width = 340 };
        AutomationProperties.SetName(_confirmation, "Confirmer le mot de passe");
        _error = Ui.Text(string.Empty, TextRole.Caption);
        _error.Foreground = Theme.Brush(palette => palette.Danger);

        var ok = Ui.ActionButton("Valider", isDefault: true);
        ok.Click += (_, _) => Validate();
        var cancel = Ui.ActionButton("Annuler", isCancel: true);

        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 22), Width = 340 };
        panel.Children.Add(Ui.Header(
            Icons.Lock,
            isNew ? "Protéger l'export" : "Ouvrir un export",
            isNew
                ? $"Au moins {ProfileArchive.MinPasswordLength} caractères. Il sera demandé pour importer ta fiche sur un autre PC : sans lui, l'export est illisible."
                : "Le mot de passe choisi au moment de l'export."));
        panel.Children.Add(Ui.Field("_Mot de passe", _password));
        if (isNew)
        {
            panel.Children.Add(Ui.Field("_Confirmer le mot de passe", _confirmation));
        }

        panel.Children.Add(_error);
        panel.Children.Add(Ui.ButtonRow(cancel, ok));
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
