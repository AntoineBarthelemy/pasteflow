using PasteFlow.App.Tests.TestWindows;
using PasteFlow.App.Tray;

namespace PasteFlow.App.Tests.Tray;

public sealed class MenuWindowTests : IDisposable
{
    private readonly List<string> _run = new();
    private readonly WindowHost<MenuWindow> _host;

    public MenuWindowTests()
    {
        _host = new WindowHost<MenuWindow>(() => new MenuWindow(new MenuEntry[]
        {
            new('F', "Ma fiche…", () => _run.Add("fiche")),
            new('A', "Annuler le dernier ajout à ma fiche", () => _run.Add("annuler"), IsEnabled: false),
            new('Q', "Quitter PasteFlow", () => _run.Add("quitter")),
        }));
    }

    [Fact]
    public void Lines_ShowTheLetterOfEachEntry()
    {
        Assert.Equal(
            new[] { "F   Ma fiche…", "A   Annuler le dernier ajout à ma fiche", "Q   Quitter PasteFlow" },
            _host.Invoke(menu => menu.Lines));
    }

    [Fact]
    public void RunLetter_RunsTheEntryWhateverTheCase()
    {
        Assert.True(_host.Invoke(menu => menu.RunLetter('f')));

        Assert.Equal(new[] { "fiche" }, _run);
    }

    [Fact]
    public void RunLetter_LeavesADisabledEntryAlone()
    {
        Assert.False(_host.Invoke(menu => menu.RunLetter('A')));

        Assert.Empty(_run);
    }

    [Fact]
    public void RunLetter_IgnoresALetterWithoutEntry()
    {
        Assert.False(_host.Invoke(menu => menu.RunLetter('Z')));

        Assert.Empty(_run);
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}
