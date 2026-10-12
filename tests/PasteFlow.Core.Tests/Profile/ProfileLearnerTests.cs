using PasteFlow.Core.History;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Security;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Profile;

public sealed class ProfileLearnerTests : IDisposable
{
    private const string Iban = "FR76 3000 6000 0112 3456 7890 189";

    private static readonly PageContext SignUpPage = new() { Title = "Créer un compte", Host = "moodle.univ-reims.fr" };

    private readonly FakeProfileStore _store = new();
    private readonly FakeProfileStore _pendingStore = new();
    private readonly FakePresenceCheck _presence = new();
    private readonly FakeLearningPrompts _prompts = new();
    private readonly ProfileBook _book;
    private readonly SensitiveGate _gate;
    private readonly ProfileLearner _learner;

    public ProfileLearnerTests()
    {
        _book = new ProfileBook(_store);
        _gate = new SensitiveGate(_presence);
        _learner = new ProfileLearner(_book, _pendingStore, _gate, _prompts);
    }

    [Fact]
    public async Task ValueWrittenTwiceIntoAFieldAboutTheUser_IsLearned()
    {
        await LearnAsync("Votre e-mail", "antoine.b@orange.fr");
        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));

        await LearnAsync("Votre e-mail", "Antoine.B@orange.fr ");

        var value = Assert.Single(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Equal(ProfileTopic.Email, value.Topic);
        Assert.Equal(ProfileOrigin.Learned, value.Origin);
        Assert.Equal(value.Id, Assert.Single(_prompts.Added).Id);
        Assert.Empty(_pendingStore.Saved);
    }

    [Fact]
    public async Task UndoOfTheAddition_TakesTheValueBackOut()
    {
        await LearnAsync("Votre e-mail", "antoine.b@orange.fr");
        await LearnAsync("Votre e-mail", "antoine.b@orange.fr");

        await _prompts.Undos[0]();

        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NeutralFieldOnASignUpPage_IsLearned()
    {
        await LearnAsync("E-mail", "antoine.b@orange.fr", SignUpPage);
        await LearnAsync("E-mail", "antoine.b@orange.fr", SignUpPage);

        var value = Assert.Single(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Equal("univ-reims.fr=1", value.Sites);
    }

    [Theory]
    [InlineData("E-mail")]
    [InlineData("E-mail du contact")]
    [InlineData("Email du parrain")]
    public async Task FieldNotKnownToBeTheUsers_TeachesNothing(string label)
    {
        await LearnAsync(label, "lea.martin@free.fr");
        await LearnAsync(label, "lea.martin@free.fr");
        await LearnAsync(label, "lea.martin@free.fr");

        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UndoneValue_NeedsAnotherUse()
    {
        await LearnAsync("Votre ville", "Taissy");
        await _learner.ForgetAsync(new[] { Written("Votre ville", "Taissy") }, CancellationToken.None);
        await LearnAsync("Votre ville", "Taissy");

        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));

        await LearnAsync("Votre ville", "Taissy");
        Assert.Equal(ProfileTopic.City, Assert.Single(await _book.GetValuesAsync(CancellationToken.None)).Topic);
    }

    [Fact]
    public async Task ValueAlreadyInTheProfile_CountsItsUseAndSite()
    {
        _ = await _book.AddAsync(ProfileTopic.Email, "antoine.b@orange.fr", string.Empty, ProfileOrigin.Manual, CancellationToken.None);

        await LearnAsync("E-mail", "antoine.b@orange.fr", SignUpPage);
        await LearnAsync("Adresse e-mail", "antoine.b@orange.fr", new PageContext { Host = "github.com" });

        var value = Assert.Single(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Equal(2, value.UseCount);
        Assert.Equal("github.com=1 univ-reims.fr=1", value.Sites);
        Assert.Empty(_prompts.Added);
    }

    [Fact]
    public async Task WhenOff_NothingIsLearnedNorCounted()
    {
        _learner.IsEnabled = false;

        await LearnAsync("Votre e-mail", "antoine.b@orange.fr");
        await LearnAsync("Votre e-mail", "antoine.b@orange.fr");

        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Empty(_pendingStore.Saved);
    }

    [Fact]
    public async Task SensitiveValue_IsAskedThenAddedBehindWindowsHello()
    {
        await LearnSensitiveAsync("Votre IBAN", Iban);
        Assert.Empty(_prompts.Asked);

        await LearnSensitiveAsync("Votre IBAN", Iban);

        Assert.Single(_prompts.Asked);
        Assert.Equal(1, _presence.Count);
        Assert.Equal(ProfileTopic.Iban, Assert.Single(await _book.GetValuesAsync(CancellationToken.None)).Topic);
        Assert.Empty(_pendingStore.Saved);
    }

    [Fact]
    public async Task SensitiveValue_RefusedByTheUser_IsNotAdded()
    {
        _prompts.Answer = false;

        await LearnSensitiveAsync("Votre IBAN", Iban);
        await LearnSensitiveAsync("Votre IBAN", Iban);

        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Equal(0, _presence.Count);
    }

    [Fact]
    public async Task SensitiveValue_WithoutWindowsHello_IsNotAdded()
    {
        _presence.Answer = PresenceResult.Unavailable;

        await LearnSensitiveAsync("Votre IBAN", Iban);
        await LearnSensitiveAsync("Votre IBAN", Iban);

        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SensitiveValue_IsNeverLearnedThroughTheOrdinaryWay()
    {
        await LearnAsync("Votre IBAN", Iban);
        await LearnAsync("Votre IBAN", Iban);

        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Empty(_pendingStore.Saved);
    }

    [Fact]
    public async Task Observe_LearnsFromTheHistory()
    {
        using var history = new FillHistory(new FakeHistoryStore());
        _learner.Observe(history, new FillMemory());

        await history.RecordAsync(new[] { Written("Votre téléphone", "06 47 21 85 39") }, CancellationToken.None);
        await history.RecordAsync(new[] { Written("Votre téléphone", "06 47 21 85 39") }, CancellationToken.None);

        Assert.Equal(ProfileTopic.Phone, Assert.Single(await _book.GetValuesAsync(CancellationToken.None)).Topic);
    }

    [Fact]
    public async Task ImportWindowsAccount_AddsWhatWindowsKnows()
    {
        var added = await _learner.ImportWindowsAccountAsync(
            new WindowsAccount { FirstName = "Antoine", LastName = "Barthélémy", Email = "antoine.b@outlook.fr" },
            CancellationToken.None);

        Assert.Equal(3, added);
        Assert.All(await _book.GetValuesAsync(CancellationToken.None), value => Assert.Equal(ProfileOrigin.WindowsAccount, value.Origin));
    }

    [Fact]
    public async Task ImportWindowsAccount_SkipsWhatIsMissingOrWrong()
    {
        var added = await _learner.ImportWindowsAccountAsync(new WindowsAccount { FirstName = "Antoine", Email = "pas-un-mail" }, CancellationToken.None);

        Assert.Equal(1, added);
        Assert.Equal(0, await _learner.ImportWindowsAccountAsync(new WindowsAccount { FirstName = "Antoine" }, CancellationToken.None));
    }

    [Fact]
    public async Task ImportIdentity_AsksBeforeTheSensitiveValues()
    {
        var added = await _learner.ImportIdentityAsync(
            new[]
            {
                new ProfileValue { Topic = ProfileTopic.City, Value = "Taissy" },
                new ProfileValue { Topic = ProfileTopic.Iban, Value = Iban },
            },
            ProfileOrigin.PasswordManager,
            CancellationToken.None);

        Assert.Equal(2, added);
        Assert.Equal(ProfileTopic.Iban, Assert.Single(_prompts.Asked).Topic);
        Assert.All(await _book.GetValuesAsync(CancellationToken.None), value => Assert.Equal(ProfileOrigin.PasswordManager, value.Origin));
    }

    [Theory]
    [InlineData("Votre e-mail", "a@b.fr", ProfileTopic.Email)]
    [InlineData("Téléphone portable", "0647218539", ProfileTopic.Phone)]
    [InlineData("Prénom", "Antoine", ProfileTopic.FirstName)]
    [InlineData("Nom", "Barthélémy", ProfileTopic.LastName)]
    [InlineData("Code postal", "51500", ProfileTopic.PostalCode)]
    [InlineData("Ville", "Taissy", ProfileTopic.City)]
    [InlineData("Profil LinkedIn", "linkedin.com/in/antoine", ProfileTopic.LinkedIn)]
    [InlineData("Site web", "antoinebarthelemy.dev", ProfileTopic.Website)]
    [InlineData("Date de naissance", "12/03/2005", ProfileTopic.BirthDate)]
    [InlineData("École", "IUT de Reims", ProfileTopic.School)]
    [InlineData("Établissement", "IUT de Reims", ProfileTopic.School)]
    [InlineData("Poste occupé", "Développeur web", ProfileTopic.JobTitle)]
    [InlineData("Adresse", "12 rue des Vignes", ProfileTopic.Street)]
    [InlineData("IBAN", Iban, ProfileTopic.Iban)]
    [InlineData("N° de sécurité sociale", "1 90 03 51 454 123 62", ProfileTopic.SocialSecurityNumber)]
    [InlineData("Numéro de passeport", "18AB12345", ProfileTopic.PassportNumber)]
    public void TopicOf_RecognizesWhatTheFieldAsks(string label, string value, ProfileTopic expected)
    {
        Assert.Equal(expected, ProfileLearner.TopicOf(TestFields.Text("f", label), value));
    }

    [Theory]
    [InlineData("Nom", "Antoine Barthélémy")]
    [InlineData("Date de livraison", "12/03/2026")]
    [InlineData("Message", "Bonjour")]
    [InlineData("Confirmez votre e-mail", "a@b.fr")]
    public void TopicOf_OfOtherFields_IsNothing(string label, string value)
    {
        Assert.Null(ProfileLearner.TopicOf(TestFields.Text("f", label), value));
    }

    public void Dispose()
    {
        _learner.Dispose();
        _gate.Dispose();
        _book.Dispose();
    }

    private static WrittenField Written(string label, string value, PageContext? page = null)
    {
        return new WrittenField { Field = TestFields.Text("f", label), Value = value, Page = page ?? PageContext.Empty };
    }

    private Task LearnAsync(string label, string value, PageContext? page = null)
    {
        return _learner.LearnAsync(new[] { Written(label, value, page) }, CancellationToken.None);
    }

    private Task LearnSensitiveAsync(string label, string value)
    {
        return _learner.LearnSensitiveAsync(new[] { Written(label, value) }, CancellationToken.None);
    }
}
