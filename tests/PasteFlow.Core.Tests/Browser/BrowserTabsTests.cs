using PasteFlow.Core.Browser;
using PasteFlow.Core.Screen;

namespace PasteFlow.Core.Tests.Browser;

public sealed class BrowserTabsTests
{
    private const string Answer = """
        {"type":"tabs","id":7,"tabs":[
          {"title":"Ancien onglet","url":"https://exemple.fr/","text":"Vieux texte","links":[],"lastAccessed":100},
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
    }
}
