using System.Windows.Automation;
using PasteFlow.App.Settings;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Settings;

namespace PasteFlow.App.Tests.Settings;

public sealed class SettingsWindowTests : IDisposable
{
    private static readonly AppSettings Current = new() { Prudence = Prudence.Balanced, LearnFromFills = true, StartWithWindows = false };

    private readonly WindowHost<SettingsWindow> _host;
    private AppSettings? _saved;

    public SettingsWindowTests()
    {
        _host = new WindowHost<SettingsWindow>(() => new SettingsWindow(Current, settings => _saved = settings));
    }

    [Fact]
    public void Window_ShowsTheCurrentSettings()
    {
        Assert.Equal(Current, _host.Invoke(window => window.Chosen));
    }

    [Fact]
    public void Save_GivesTheSettingsChosenAndCloses()
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        Select(window, "Prudent");
        Toggle(window, "Apprendre de mes remplissages");
        Toggle(window, "Lancer PasteFlow au démarrage de Windows");

        Click(window, "Enregistrer");

        WaitUntil(() => _saved is not null);
        Assert.Equal(new AppSettings { Prudence = Prudence.Cautious, LearnFromFills = false, StartWithWindows = true }, _saved);
        WaitUntil(() => _host.IsClosed);
    }

    [Fact]
    public void Cancel_SavesNothing()
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        Select(window, "Audacieux");

        Click(window, "Annuler");

        WaitUntil(() => _host.IsClosed);
        Assert.Null(_saved);
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static AutomationElement Find(AutomationElement window, string nameStart)
    {
        var element = window.FindAll(TreeScope.Descendants, Condition.TrueCondition)
            .Cast<AutomationElement>()
            .FirstOrDefault(candidate => candidate.Current.Name.StartsWith(nameStart, StringComparison.Ordinal));
        Assert.NotNull(element);
        return element;
    }

    private static void Select(AutomationElement window, string nameStart)
    {
        ((SelectionItemPattern)Find(window, nameStart).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }

    private static void Toggle(AutomationElement window, string nameStart)
    {
        ((TogglePattern)Find(window, nameStart).GetCurrentPattern(TogglePattern.Pattern)).Toggle();
    }

    private static void Click(AutomationElement window, string nameStart)
    {
        ((InvokePattern)Find(window, nameStart).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            Thread.Sleep(20);
        }
    }
}
