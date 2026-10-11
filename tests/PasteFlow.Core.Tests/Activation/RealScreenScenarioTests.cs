using PasteFlow.Core.Activation;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.History;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Activation;

/// <summary>
/// Whole fills from realistic screens, with the real official lists.
/// </summary>
public sealed class RealScreenScenarioTests : IDisposable
{
    private const string Signature = """
        Bonjour,

        Merci pour votre commande CMD-48213, elle sera livrée le 14/10/2026.

        Cordialement,
        Marie Lefèvre
        Responsable achats
        Boulangerie Martin SARL
        14 avenue Jean Jaurès
        51100 Reims
        France
        Tél. 03 26 47 21 85
        marie.lefevre@boulangerie-martin.fr
        """;

    private readonly FakeHotkeyListener _listener = new();
    private readonly FakeFieldWriter _fields = new();
    private readonly FakeUserNotifier _notifier = new();

    [Fact]
    public void Fill_FromAnEmailSignature_FillsTheWholeContactForm()
    {
        var form = new[]
        {
            TestFields.Text("first", "Prénom"),
            TestFields.Text("last", "Nom"),
            TestFields.Text("company", "Société"),
            TestFields.Text("email", "Adresse e-mail"),
            TestFields.Text("phone", "Téléphone"),
            TestFields.Text("postal", "Code postal"),
            TestFields.Text("city", "Ville"),
            TestFields.Text("country", "Pays"),
            TestFields.Text("order", "N° de commande"),
            TestFields.Text("message", "Votre message"),
        };

        Fill(form, Signature);

        Assert.Equal("Marie", _fields.WrittenValues["first"]);
        Assert.Equal("Lefèvre", _fields.WrittenValues["last"]);
        Assert.Equal("Boulangerie Martin SARL", _fields.WrittenValues["company"]);
        Assert.Equal("marie.lefevre@boulangerie-martin.fr", _fields.WrittenValues["email"]);
        Assert.Equal("03 26 47 21 85", _fields.WrittenValues["phone"]);
        Assert.Equal("51100", _fields.WrittenValues["postal"]);
        Assert.Equal("Reims", _fields.WrittenValues["city"]);
        Assert.Equal("France", _fields.WrittenValues["country"]);
        Assert.Equal("CMD-48213", _fields.WrittenValues["order"]);
        Assert.False(_fields.WrittenValues.ContainsKey("message"));
    }

    [Fact]
    public void Fill_WithASingleNameField_PutsTheWholeName()
    {
        Fill(new[] { TestFields.Text("name", "Nom"), TestFields.Text("email", "Email") }, Signature);

        Assert.Equal("Marie Lefèvre", _fields.WrittenValues["name"]);
    }

    [Fact]
    public void Fill_WithPostalCodeOfAnotherCommune_LeavesBothToCheck()
    {
        const string Screen = """
            Livraison :
            Antoine Barthélémy
            3 rue des Vignes
            51500 Reims
            """;

        Fill(new[] { TestFields.Text("postal", "Code postal"), TestFields.Text("city", "Ville") }, Screen + "\nVille : Reims");

        Assert.Empty(_fields.WrittenValues);
    }

    [Theory]
    [InlineData("Antoine Barthélémy", "Antoine", "Barthélémy")]
    [InlineData("BARTHÉLÉMY Antoine", "Antoine", "BARTHÉLÉMY")]
    [InlineData("Pierre Martin", "Pierre", "Martin")]
    [InlineData("Jean-Pierre Dupont", "Jean-Pierre", "Dupont")]
    public void Fill_SplitsNamesThatAreAlsoFirstNames(string person, string firstName, string lastName)
    {
        Fill(new[] { TestFields.Text("first", "Prénom"), TestFields.Text("last", "Nom") }, $"Cordialement,\n{person}");

        Assert.Equal(firstName, _fields.WrittenValues["first"]);
        Assert.Equal(lastName, _fields.WrittenValues["last"]);
    }

    [Fact]
    public void Fill_AFirstNameThatIsAlsoACountryStaysAFirstName()
    {
        Fill(
            new[] { TestFields.Text("first", "Prénom"), TestFields.Text("country", "Pays") },
            "Prénom : Maurice\nNom : Durand");

        Assert.Equal("Maurice", _fields.WrittenValues["first"]);
        Assert.False(_fields.WrittenValues.ContainsKey("country"));
    }

    [Fact]
    public void Fill_NeverTakesAnInvoiceHeadingForACommune()
    {
        Fill(new[] { TestFields.Text("city", "Ville") }, "FACTURE\nTotal : 49,90 €");

        Assert.Empty(_fields.WrittenValues);
    }

    [Fact]
    public void Fill_NeverTakesTableHeadersForValues()
    {
        const string Screen = """
            NOM          PRÉNOM       VILLE
            Commande     Statut       Total
            """;

        Fill(new[] { TestFields.Text("city", "Ville"), TestFields.Text("first", "Prénom"), TestFields.Text("last", "Nom") }, Screen);

        Assert.Empty(_fields.WrittenValues);
    }

    [Fact]
    public async Task Fill_LearnsAValueUsedTwiceAndFillsItWhenTheScreenSaysNothing()
    {
        using var history = new FillHistory(new FakeHistoryStore());
        var memory = new FillMemory();
        history.Observe(memory);
        var form = new[] { TestFields.Text("member", "Numéro adhérent"), TestFields.Text("email", "Email") };

        // Twice the number is on screen and filled, the third time the screen only shows an email
        FillLearning(form, "Numéro adhérent : A-4521", history, memory);
        FillLearning(form, "Numéro adhérent : A-4521", history, memory);
        _fields.SimulateUserTyping("member", string.Empty);
        FillLearning(form, "Contact : antoine.b@orange.fr", history, memory);

        await Task.Yield();
        Assert.Equal("A-4521", _fields.CurrentValues["member"]);
        Assert.Equal("antoine.b@orange.fr", _fields.CurrentValues["email"]);
        Assert.Contains("Numéro adhérent : A-4521 (Historique)", _notifier.Messages[^1], StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    [Fact]
    public void Fill_CopiesAFormIntoAnotherUnderTheSameLabels()
    {
        const string Description = "Dans cette vidéo, je montre PasteFlow.\n\nLe code : https://github.com/AntoineBarthelemy/pasteflow\nContact : antoine@exemple.fr";
        var source = new WindowText
        {
            Title = "Détails de la vidéo - YouTube Studio",
            Text = "Détails de la vidéo\nTitle (required)\nMon tuto PasteFlow\nDescription\n" + Description + "\nVisibilité\nPublique",
            Rank = 1,
            Fields = new[]
            {
                new FieldText { Label = "Search across your channel", Value = "tuto" },
                new FieldText { Label = "Title (required)", Value = "Mon tuto PasteFlow" },
                new FieldText { Label = "Description", Value = Description },
            },
        };
        var form = new[]
        {
            TestFields.Text("search", "Search across your channel"),
            TestFields.Text("title", "Title (required)"),
            TestFields.Text("description", "Description"),
        };

        Fill(form, source);

        Assert.Equal("Mon tuto PasteFlow", _fields.WrittenValues["title"]);
        Assert.Equal(Description, _fields.WrittenValues["description"]);
        Assert.False(_fields.WrittenValues.ContainsKey("search"));
    }

    [Fact]
    public void Fill_FromAFilledContactForm_FillsTheSameFieldsOfAnotherForm()
    {
        var source = new WindowText
        {
            Title = "Ancienne commande",
            Text = "Vos coordonnées",
            Rank = 1,
            Fields = new[]
            {
                new FieldText { Label = "Prénom", Value = "Marie" },
                new FieldText { Label = "Nom", Value = "Lefèvre" },
                new FieldText { Label = "Adresse e-mail", Value = "marie.lefevre@boulangerie-martin.fr" },
                new FieldText { Label = "Ville", Value = "Reims" },
            },
        };
        var form = new[]
        {
            TestFields.Text("first", "First name"),
            TestFields.Text("last", "Last name"),
            TestFields.Text("email", "Email"),
            TestFields.Text("city", "City"),
        };

        Fill(form, source);

        Assert.Equal("Marie", _fields.WrittenValues["first"]);
        Assert.Equal("Lefèvre", _fields.WrittenValues["last"]);
        Assert.Equal("marie.lefevre@boulangerie-martin.fr", _fields.WrittenValues["email"]);
        Assert.Equal("Reims", _fields.WrittenValues["city"]);
    }

    [Fact]
    public void Fill_FromAnotherForm_LeavesFieldsWithoutAMatchingLabelEmpty()
    {
        var source = new WindowText
        {
            Title = "Studio",
            Text = string.Empty,
            Rank = 1,
            Fields = new[]
            {
                new FieldText { Label = "Title (required)", Value = "Mon tuto PasteFlow" },
                new FieldText { Label = "Description", Value = "Une description" },
            },
        };

        Fill(new[] { TestFields.Text("message", "Votre message"), TestFields.Text("company", "Société") }, source);

        Assert.Empty(_fields.WrittenValues);
    }

    private void FillLearning(IReadOnlyList<FormField> form, string screen, FillHistory history, FillMemory memory)
    {
        var reader = new FakeWindowTextReader
        {
            Windows = new[] { new WindowText { Title = "Gmail", Text = screen, Rank = 1 } },
        };
        using var listener = new FakeHotkeyListener();
        using var service = new ShortcutActivationService(
            listener,
            new FakeFormReader { Fields = form },
            _fields,
            new ICandidateSource[] { new ScreenCandidateSource(reader), history },
            _notifier,
            memory,
            new FakeFieldHighlighter());
        service.Start(Hotkey.Default);

        listener.SimulatePress();
    }

    private void Fill(IReadOnlyList<FormField> form, string screen)
    {
        Fill(form, new WindowText { Title = "Gmail", Text = screen, Rank = 1 });
    }

    private void Fill(IReadOnlyList<FormField> form, WindowText screen)
    {
        var reader = new FakeWindowTextReader { Windows = new[] { screen } };
        using var service = new ShortcutActivationService(
            _listener,
            new FakeFormReader { Fields = form },
            _fields,
            new ICandidateSource[] { new ScreenCandidateSource(reader) },
            _notifier);
        service.Start(Hotkey.Default);

        _listener.SimulatePress();
    }
}
