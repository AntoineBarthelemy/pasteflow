using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace PasteFlow.App.Tests.TestWindows;

/// <summary>
/// A small form covering the kinds of fields the application meets: empty, filled, labelled by another element,
/// password, list of choices and disabled, plus some text to read.
/// </summary>
internal sealed class ContactFormWindow : Window
{
    public const string WindowTitle = "Formulaire de test PasteFlow";
    public const string PasswordValue = "secret-password";
    public const string OrderText = "Commande n° CMD-48213";
    public const string TotalText = "Total : 49,90 €";
    public const string EmailHint = "ex : jean.dupont@exemple.fr";

    public ContactFormWindow()
    {
        Title = WindowTitle;
        Left = 0;
        Top = 0;
        Width = 420;
        Height = 520;
        ShowActivated = false;
        ShowInTaskbar = false;

        NameBox = new TextBox();
        AutomationProperties.SetName(NameBox, "Nom");

        EmailBox = new TextBox { Text = "antoine@example.com" };
        AutomationProperties.SetName(EmailBox, "Email");
        AutomationProperties.SetHelpText(EmailBox, EmailHint);

        var cityLabel = new Label { Content = "Ville" };
        CityBox = new TextBox();
        AutomationProperties.SetLabeledBy(CityBox, cityLabel);

        PasswordBox = new PasswordBox { Password = PasswordValue };
        AutomationProperties.SetName(PasswordBox, "Mot de passe");

        CountryBox = new ComboBox { ItemsSource = new[] { "France", "Belgique" } };
        AutomationProperties.SetName(CountryBox, "Pays");

        DisabledBox = new TextBox { IsEnabled = false };
        AutomationProperties.SetName(DisabledBox, "Désactivé");

        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = OrderText });
        panel.Children.Add(new TextBlock { Text = TotalText });
        panel.Children.Add(NameBox);
        panel.Children.Add(EmailBox);
        panel.Children.Add(cityLabel);
        panel.Children.Add(CityBox);
        panel.Children.Add(PasswordBox);
        panel.Children.Add(CountryBox);
        panel.Children.Add(DisabledBox);
        Content = panel;
    }

    public TextBox NameBox { get; }

    public TextBox EmailBox { get; }

    public TextBox CityBox { get; }

    public PasswordBox PasswordBox { get; }

    public ComboBox CountryBox { get; }

    public TextBox DisabledBox { get; }
}
