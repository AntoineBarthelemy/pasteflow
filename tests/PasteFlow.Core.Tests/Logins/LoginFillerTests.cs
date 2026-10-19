using PasteFlow.Core.Forms;
using PasteFlow.Core.Logins;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Security;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Logins;

public sealed class LoginFillerTests : IDisposable
{
    private static readonly FormField Identifier = TestFields.Text("login", "E-mail ou nom d'utilisateur");
    private static readonly FormField Password = TestFields.Password("password", "Mot de passe");
    private static readonly VaultLogin GitHub = new() { Id = "gh", Name = "GitHub", UserName = "AntoineBarthelemy", Uris = new[] { "https://github.com/login" } };
    private static readonly VaultLogin GitHubSchool = new() { Id = "gh2", Name = "GitHub IUT", UserName = "abarthelemy-iut", Uris = new[] { "github.com" } };
    private static readonly VaultLogin Steam = new() { Id = "steam", Name = "Steam", UserName = "antoine51", Uris = Array.Empty<string>() };

    private readonly FakePasswordManager _manager = new();
    private readonly FakePresenceCheck _presence = new();
    private readonly FakeLoginPicker _picker = new();
    private readonly FakeFieldWriter _writer = new();
    private readonly SensitiveGate _gate;

    public LoginFillerTests()
    {
        _gate = new SensitiveGate(_presence);
        _manager.Logins.AddRange(new[] { GitHub, Steam });
        _manager.Passwords["gh"] = "mot-de-passe-github";
        _manager.Passwords["gh2"] = "mot-de-passe-iut";
        _manager.Passwords["steam"] = "mot-de-passe-steam";
    }

    [Fact]
    public async Task SiteWithOneAccount_FillsIdentifierAndPasswordAfterWindowsHello()
    {
        var fill = await FillAsync(new PageContext { Host = "github.com", Program = "chrome" });

        Assert.NotNull(fill);
        Assert.True(fill.IsPasswordWritten);
        Assert.Equal("AntoineBarthelemy", _writer.WrittenValues["login"]);
        Assert.Equal("mot-de-passe-github", _writer.WrittenValues["password"]);
        Assert.Equal(1, _presence.Count);
        Assert.Null(_picker.Shown);
    }

    [Theory]
    [InlineData("github.com.fake.ru")]
    [InlineData("githuh.com")]
    public async Task LookAlikeSite_GetsNothing(string host)
    {
        var fill = await FillAsync(new PageContext { Host = host, Program = "chrome" });

        Assert.Null(fill);
        Assert.Empty(_writer.WrittenValues);
        Assert.Equal(0, _manager.PasswordReads);
    }

    [Fact]
    public async Task SiteWithSeveralAccounts_LetsTheUserChoose()
    {
        _manager.Logins.Add(GitHubSchool);
        _picker.Choose = logins => logins.Single(login => login.Id == "gh2");

        var fill = await FillAsync(new PageContext { Host = "github.com" });

        Assert.Equal(2, _picker.Shown!.Count);
        Assert.Equal("abarthelemy-iut", _writer.WrittenValues["login"]);
        Assert.Equal("mot-de-passe-iut", _writer.WrittenValues["password"]);
        Assert.Equal("GitHub IUT", fill!.AccountName);
    }

    [Fact]
    public async Task SiteWithSeveralAccounts_WithoutPicker_FillsNothing()
    {
        _manager.Logins.Add(GitHubSchool);
        var filler = new LoginFiller(_manager, _gate, null, _writer);

        Assert.Null(await filler.FillAsync(new[] { Identifier, Password }, new PageContext { Host = "github.com" }, CancellationToken.None));
        Assert.Empty(_writer.WrittenValues);
    }

    [Fact]
    public async Task WithoutWindowsHello_OnlyTheIdentifierIsWritten()
    {
        _presence.Answer = PresenceResult.Refused;

        var fill = await FillAsync(new PageContext { Host = "github.com" });

        Assert.False(fill!.IsPasswordWritten);
        Assert.Equal("AntoineBarthelemy", _writer.WrittenValues["login"]);
        Assert.False(_writer.WrittenValues.ContainsKey("password"));
        Assert.DoesNotContain(_writer.WrittenValues.Values, value => value.Contains("mot-de-passe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Application_AlwaysLetsTheUserChoose()
    {
        var fill = await FillAsync(new PageContext { Program = "steam" });

        Assert.Equal(new[] { Steam }, _picker.Shown);
        Assert.Equal("mot-de-passe-steam", _writer.WrittenValues["password"]);
        Assert.True(fill!.IsPasswordWritten);
    }

    [Fact]
    public async Task Application_WhenTheUserCancels_GetsNothing()
    {
        _picker.Choose = _ => null;

        Assert.Null(await FillAsync(new PageContext { Program = "steam" }));
        Assert.Empty(_writer.WrittenValues);
    }

    [Fact]
    public async Task BrowserWithoutReadableAddress_GetsNothing()
    {
        Assert.Null(await FillAsync(new PageContext { Program = "chrome" }));
        Assert.Null(_picker.Shown);
    }

    [Fact]
    public async Task LockedVault_GetsNothing()
    {
        _manager.IsUnlocked = false;

        Assert.Null(await FillAsync(new PageContext { Host = "github.com" }));
    }

    [Fact]
    public async Task IdentifierAlreadyThere_IsLeftToWhoeverWroteIt()
    {
        var filled = Identifier with { HasValue = true };
        var filler = new LoginFiller(_manager, _gate, _picker, _writer);

        Assert.Null(await filler.FillAsync(new[] { filled, Password }, new PageContext { Host = "github.com" }, CancellationToken.None));
    }

    [Fact]
    public void FormWithSeveralPasswords_IsNotASignIn()
    {
        var fields = new[] { Identifier, Password, TestFields.Password("confirm", "Confirmez le mot de passe") };

        Assert.Null(LoginFiller.SignInFields(fields));
    }

    [Fact]
    public void NewPasswordField_IsNotASignIn()
    {
        Assert.Null(LoginFiller.SignInFields(new[] { Identifier, TestFields.Password("new", "Nouveau mot de passe") }));
    }

    [Fact]
    public void SignInFields_TakeTheIdentifierBeforeThePassword()
    {
        var fields = new[] { TestFields.Text("search", "Rechercher"), Identifier, Password, TestFields.Text("code", "Code promo") };

        var signIn = LoginFiller.SignInFields(fields);

        Assert.Equal("login", signIn!.Value.Identifier!.Id);
        Assert.Equal("password", signIn.Value.Password.Id);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private Task<LoginFill?> FillAsync(PageContext page)
    {
        return new LoginFiller(_manager, _gate, _picker, _writer).FillAsync(new[] { Identifier, Password }, page, CancellationToken.None);
    }
}
