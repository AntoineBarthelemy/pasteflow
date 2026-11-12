using PasteFlow.Core.Browser;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Tests.Browser;

public sealed class BrowserTabsTests
{
    private const string Answer = """
        {"type":"tabs","id":7,"tabs":[
          {"title":"Ancien onglet","url":"https://exemple.fr/","text":"Vieux texte","links":[],"fields":[{"label":"Titre (obligatoire)","value":"Mon tuto PasteFlow"},{"label":"","value":"sans étiquette"},{"label":"Vide","value":"  "}],"lastAccessed":100},
          {"title":"Boîte de réception - Gmail","url":"https://mail.google.com/","text":"Léa : lea.martin@atelier-nova.fr","links":[{"text":"Ma vidéo","href":"https://youtu.be/abc"},{"text":"","href":"https://vide.fr"}],"lastAccessed":300},
          {"title":"Formulaire - Inscription","url":"https://exemple.fr/inscription","text":"Nom\nE-mail","links":[],"lastAccessed":200},
          "pas un onglet"
        ]}
        """;

    [Fact]
    public void Request_AsksForTheRecentTabs()
    {
        Assert.Equal("""{"type":"read","id":3,"max":5}""", BrowserTabs.Request(3));
    }

    [Fact]
    public void AnswerId_FindsTheRequestAnswered()
    {
        Assert.Equal(7, BrowserTabs.AnswerId(Answer));
        Assert.Null(BrowserTabs.AnswerId("""{"type":"hello"}"""));
        Assert.Null(BrowserTabs.AnswerId("cassé"));
    }

    [Fact]
    public void Read_GivesTheTabsMostRecentFirstWithTheirLinks()
    {
        var tabs = BrowserTabs.Read(Answer);

        Assert.Equal(new[] { "Boîte de réception - Gmail", "Formulaire - Inscription", "Ancien onglet" }, tabs.Select(tab => tab.Title));
        Assert.Equal(new LinkText { Text = "Ma vidéo", Address = "https://youtu.be/abc" }, Assert.Single(tabs[0].Links));
    }

    [Fact]
    public void Read_KeepsTheFilledFieldsWithALabel()
    {
        var tab = BrowserTabs.Read(Answer).Single(read => read.Title == "Ancien onglet");

        Assert.Equal(new FieldText { Label = "Titre (obligatoire)", Value = "Mon tuto PasteFlow" }, Assert.Single(tab.Fields));
    }

    [Fact]
    public void Merge_AddsTheHiddenTabsAfterTheWindowsAndLeavesTheVisibleOnesToTheScreen()
    {
        var windows = new[]
        {
            new WindowText { Title = "Formulaire - Inscription - Google Chrome", Text = "Nom\nE-mail", Rank = 0 },
            new WindowText { Title = "Bloc-notes", Text = "Notes", Rank = 1 },
        };

        var merged = BrowserTabs.Merge(windows, BrowserTabs.Read(Answer));

        Assert.Equal(new[] { "Formulaire - Inscription - Google Chrome", "Bloc-notes", "Boîte de réception - Gmail", "Ancien onglet" }, merged.Select(window => window.Title));
        Assert.Equal(new[] { 0, 1, 2, 3 }, merged.Select(window => window.Rank));
        Assert.Equal("https://mail.google.com/", merged[2].Address);
        Assert.Equal("Mon tuto PasteFlow", Assert.Single(merged[3].Fields).Value);
    }

    [Fact]
    public void Merge_GivesTheWindowOnScreenTheAddressOfItsTabWhenItsBarWasNotRead()
    {
        // Seen on 12/11/2026: the address bar of a sign-in page was not read, the extension knew the address
        var windows = new[] { new WindowText { Title = "CAS Login - Google Chrome", Text = "Username", Rank = 0 } };
        var tabs = new[] { new BrowserTab { Title = "CAS Login", Url = "https://cas.univ-ville.fr/cas/login?service=https%3a%2f%2fmail", Text = "Username" } };

        var merged = BrowserTabs.Merge(windows, tabs);

        var window = Assert.Single(merged);
        Assert.Equal("cas.univ-ville.fr", PageAddress.HostOf(window.Address));
    }

    [Fact]
    public void Merge_KeepsTheAddressReadInTheBar()
    {
        var windows = new[] { new WindowText { Title = "CAS Login - Google Chrome", Text = "Username", Rank = 0, Address = "cas.univ-ville.fr/cas/login" } };
        var tabs = new[] { new BrowserTab { Title = "CAS Login", Url = "https://autre-site.fr/", Text = "Username" } };

        Assert.Equal("cas.univ-ville.fr/cas/login", Assert.Single(BrowserTabs.Merge(windows, tabs)).Address);
    }

    [Fact]
    public void Merge_GivesNoAddressThatIsNotAWebPage()
    {
        var windows = new[] { new WindowText { Title = "Extensions - Google Chrome", Text = "Extensions", Rank = 0 } };
        var tabs = new[] { new BrowserTab { Title = "Extensions", Url = "chrome://extensions/", Text = "Extensions" } };

        Assert.Equal(string.Empty, Assert.Single(BrowserTabs.Merge(windows, tabs)).Address);
    }
}
