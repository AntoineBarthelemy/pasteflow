using System.IO;
using System.Windows.Automation;
using PasteFlow.App.Profile;
using PasteFlow.App.Tests.Fakes;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Tests.Profile;

public sealed class ProfileWindowTests : IDisposable
{
    private static readonly ProfileValue Email = new() { Topic = ProfileTopic.Email, Value = "antoine.b@orange.fr", Label = "Perso" };
    private static readonly ProfileValue Iban = new() { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 189" };

    private readonly string _folder = Directory.CreateTempSubdirectory("pasteflow-tests-").FullName;
    private readonly InMemoryProfileStore _store = new(Email, Iban);
    private readonly FakeProfileTransferPrompts _prompts = new();
    private readonly ProfileBook _book;
    private readonly WindowHost<ProfileWindow> _host;

    public ProfileWindowTests()
    {
        _book = new ProfileBook(_store);
        _host = new WindowHost<ProfileWindow>(() => new ProfileWindow(_book, _ => _prompts));
        WaitUntil(() => _host.Invoke(window => window.Lines.Count) == 2);
    }

    [Fact]
    public void Window_ShowsTheProfileAndHidesSensitiveValues()
    {
        var lines = _host.Invoke(window => window.Lines);

        Assert.Contains("Perso : antoine.b@orange.fr", lines);
        Assert.Contains("IBAN : •••• 0189", lines);
        Assert.DoesNotContain(lines, line => line.Contains("3000", StringComparison.Ordinal));
    }

    [Fact]
    public void Line_SaysWhereALearnedValueComesFrom()
    {
        Assert.Equal("Ville : Taissy (appris)", ProfileWindow.Line(new ProfileValue { Topic = ProfileTopic.City, Value = "Taissy", Origin = ProfileOrigin.Learned }));
    }

    [Fact]
    public void Add_KeepsTheValueTyped()
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        _ = _host.Invoke(profile =>
        {
            profile.ChooseTopic(ProfileTopic.City);
            return true;
        });
        SetText(window, "Valeur", "Taissy");

        Click(window, "Ajouter");

        WaitUntil(() => _store.Saved.Any(value => value.Topic == ProfileTopic.City && value.Value == "Taissy"));
        WaitUntil(() => _host.Invoke(profile => profile.Lines.Count) == 3);
        WaitUntil(() => _host.Invoke(profile => profile.Status) == "C'est enregistré.");
    }

    [Fact]
    public void Add_OfAWrongValue_SaysWhy()
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        _ = _host.Invoke(profile =>
        {
            profile.ChooseTopic(ProfileTopic.Other);
            return true;
        });
        SetText(window, "Valeur", "4111 1111 1111 1111");

        Click(window, "Ajouter");

        WaitUntil(() => _host.Invoke(profile => profile.Status) == ProfileMessages.For(ProfileEditResult.PaymentCardRefused));
        Assert.Equal(2, _store.Saved.Count);
    }

    [Fact]
    public void SelectingASensitiveValue_NeverPutsItOnScreen()
    {
        var window = AutomationElement.FromHandle(_host.Handle);

        SelectItem(window, "IBAN");

        WaitUntil(() => _host.Invoke(profile => profile.Status).Contains("masquée", StringComparison.Ordinal));
        Assert.Equal(string.Empty, GetText(window, "Valeur"));
    }

    [Fact]
    public void SelectingAnOrdinaryValue_ShowsItToCorrectIt()
    {
        var window = AutomationElement.FromHandle(_host.Handle);

        SelectItem(window, "Perso");

        WaitUntil(() => GetText(window, "Valeur") == "antoine.b@orange.fr");
        Assert.Equal("Perso", GetText(window, "Nom (facultatif)"));
    }

    [Fact]
    public void Correct_ChangesTheSelectedValue()
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        SelectItem(window, "Perso");
        WaitUntil(() => GetText(window, "Valeur") == "antoine.b@orange.fr");
        SetText(window, "Valeur", "antoine.barthelemy@orange.fr");

        Click(window, "Corriger");

        WaitUntil(() => _store.Saved.Any(value => value.Id == Email.Id && value.Value == "antoine.barthelemy@orange.fr"));
    }

    [Fact]
    public void Remove_RemovesTheSelectedValue()
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        SelectItem(window, "IBAN");

        Click(window, "Supprimer");

        WaitUntil(() => _store.Saved.Count == 1);
        Assert.Equal(Email.Id, _store.Saved[0].Id);
        WaitUntil(() => _host.Invoke(profile => profile.Lines.Count) == 1);
    }

    [Fact]
    public async Task ExportThenImport_BringsTheProfileBack()
    {
        var window = AutomationElement.FromHandle(_host.Handle);
        _prompts.ExportPath = Path.Combine(_folder, "ma-fiche.pasteflow");
        _prompts.ImportPath = _prompts.ExportPath;
        _prompts.Password = "vignes-de-Taissy";

        Click(window, "Exporter");
        WaitUntil(() => _host.Invoke(profile => profile.Status).StartsWith("Fiche exportée", StringComparison.Ordinal), seconds: 20);
        Assert.Equal(ProfileEditResult.Done, await _book.RemoveAsync(Email.Id, CancellationToken.None));
        Assert.Equal(ProfileEditResult.Done, await _book.RemoveAsync(Iban.Id, CancellationToken.None));

        Click(window, "Importer");

        WaitUntil(() => _host.Invoke(profile => profile.Status) == "2 infos ajoutées à ta fiche.", seconds: 20);
        Assert.Equal(2, _host.Invoke(profile => profile.Lines.Count));
        Assert.Contains(_store.Saved, value => value.Topic == ProfileTopic.Iban && value.Value == Iban.Value);
    }

    [Fact]
    public async Task Import_WithTheWrongPassword_SaysSo()
    {
        var path = Path.Combine(_folder, "ma-fiche.pasteflow");
        await File.WriteAllBytesAsync(path, ProfileArchive.Export(new[] { Email }, "vignes-de-Taissy"));
        _prompts.ImportPath = path;
        _prompts.Password = "vignes-de-Reims";

        Click(AutomationElement.FromHandle(_host.Handle), "Importer");

        WaitUntil(() => _host.Invoke(profile => profile.Status) == ProfileMessages.For(ProfileArchiveStatus.WrongPassword), seconds: 20);
    }

    [Fact]
    public void Export_CancelledByTheUser_WritesNothing()
    {
        _prompts.ExportPath = null;

        Click(AutomationElement.FromHandle(_host.Handle), "Exporter");

        Thread.Sleep(300);
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Equal(0, _prompts.PasswordQuestions);
    }

    public void Dispose()
    {
        _host.Dispose();
        _book.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private static AutomationElement Find(AutomationElement window, ControlType controlType, string nameStart)
    {
        AutomationElement? element = null;
        WaitUntil(() =>
        {
            element = window.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, controlType))
                .Cast<AutomationElement>()
                .FirstOrDefault(candidate => candidate.Current.Name.StartsWith(nameStart, StringComparison.Ordinal));
            return element is not null;
        });
        return element!;
    }

    private static void Click(AutomationElement window, string nameStart)
    {
        ((InvokePattern)Find(window, ControlType.Button, nameStart).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static void SetText(AutomationElement window, string name, string text)
    {
        ((ValuePattern)Find(window, ControlType.Edit, name).GetCurrentPattern(ValuePattern.Pattern)).SetValue(text);
    }

    private static string GetText(AutomationElement window, string name)
    {
        return ((ValuePattern)Find(window, ControlType.Edit, name).GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
    }

    private static void SelectItem(AutomationElement window, string nameStart)
    {
        ((SelectionItemPattern)Find(window, ControlType.ListItem, nameStart).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }

    private static void WaitUntil(Func<bool> condition, int seconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            Thread.Sleep(20);
        }
    }
}
