using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Logins;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Security;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Activation;

public sealed class LoginActivationTests : IDisposable
{
    private const string Secret = "mot-de-passe-github";

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeFieldWriter _fields = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakePasswordManager _manager = new();
    private readonly FakePresenceCheck _presence = new();
    private readonly SensitiveGate _gate;
    private readonly FakeCaret _caret = new();
    private readonly FillMemory _memory = new();
    private readonly List<WrittenField> _remembered = new();
    private readonly ShortcutActivationService _service;

    public LoginActivationTests()
    {
        _gate = new SensitiveGate(_presence);
        _manager.Logins.Add(new VaultLogin { Id = "gh", Name = "GitHub", UserName = "AntoineBarthelemy", Uris = new[] { "https://github.com" } });
        _manager.Passwords["gh"] = Secret;
        _memory.Filled += (_, written) => _remembered.AddRange(written);

        var page = new WindowText { Title = "Connexion à GitHub", Rank = 0, Text = "Se connecter à GitHub", Address = "github.com/login", Program = "msedge" };
        var profile = new FakeCandidateSource
        {
            Candidates = new[] { new Candidate { Value = "antoine.b@orange.fr", Kind = CandidateKind.Email, Label = "E-mail", SourceName = "Ma fiche", SourceRank = 0, IsFromProfile = true } },
        };
        _service = new ShortcutActivationService(
            _listener,
            new FakeFormReader { Fields = new[] { TestFields.Text("login", "Adresse e-mail"), TestFields.Password("password", "Mot de passe") } },
            _fields,
            new ICandidateSource[] { new ScreenCandidateSource(new FakeWindowTextReader { Windows = new[] { page } }), profile },
            _notifier,
            _memory,
            new FakeFieldHighlighter())
        {
            SensitiveGate = _gate,
            PasswordManager = _manager,
            CaretReader = _caret,
            Picker = _caret,
            Inserter = _caret,
        };
        _ = _service.Start(Hotkey.Default);
    }

    [Fact]
    public void SignInPage_IsFilledFromThePasswordManager()
    {
        _listener.SimulatePress();

        Assert.Equal("AntoineBarthelemy", _fields.WrittenValues["login"]);
        Assert.Equal(Secret, _fields.WrittenValues["password"]);
    }

    [Fact]
    public void CursorInTheIdentifier_FillsTheSignInFormRatherThanPastingAtTheCursor()
    {
        // As in a real browser: the cursor blinks in the identifier field, next to the only password field
        _caret.Caret = new CaretContext { IsEditable = true, FocusedFieldId = "login", FieldLabel = "Adresse e-mail" };

        _listener.SimulatePress();

        Assert.Null(_caret.ShownRequest);
        Assert.Empty(_caret.InsertedTexts);
        Assert.Equal("AntoineBarthelemy", _fields.WrittenValues["login"]);
        Assert.Equal(Secret, _fields.WrittenValues["password"]);
    }

    [Fact]
    public void Password_IsNeverShownNorRemembered()
    {
        _listener.SimulatePress();

        var report = Assert.Single(_notifier.Reports);
        Assert.DoesNotContain(report.FilledValues, value => value.Value.Contains(Secret, StringComparison.Ordinal));
        Assert.Contains(report.FilledValues, value => value.Value == "••••••••");
        Assert.DoesNotContain(_remembered, written => written.Value == Secret);
    }

    [Fact]
    public void WithoutPasswordManager_TheIdentifierComesFromTheProfile()
    {
        _service.PasswordManager = null;

        _listener.SimulatePress();

        Assert.Equal("antoine.b@orange.fr", _fields.WrittenValues["login"]);
        Assert.False(_fields.WrittenValues.ContainsKey("password"));
    }

    [Fact]
    public void WithoutWindowsHello_ThePasswordIsLeftToTheUser()
    {
        _presence.Answer = PresenceResult.Refused;

        _listener.SimulatePress();

        Assert.False(_fields.WrittenValues.ContainsKey("password"));
        Assert.Contains(Assert.Single(_notifier.Reports).FieldsToCheck, field => field.Field.Id == "password");
    }

    public void Dispose()
    {
        _service.Dispose();
        _gate.Dispose();
        _listener.Dispose();
    }
}
