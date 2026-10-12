using PasteFlow.Core.Candidates;
using PasteFlow.Core.History;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.History;

public sealed class FillHistoryTests : IDisposable
{
    private readonly FakeHistoryStore _store = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly FillHistory _history;

    public FillHistoryTests()
    {
        _history = new FillHistory(_store, _clock);
    }

    [Fact]
    public async Task GetCandidatesAsync_OffersAValueOnlyOnceUsedTwice()
    {
        await RecordAsync("Email", "antoine.b@orange.fr");
        Assert.Empty(await _history.GetCandidatesAsync(CancellationToken.None));

        await RecordAsync("Email", "antoine.b@orange.fr");
        var candidate = Assert.Single(await _history.GetCandidatesAsync(CancellationToken.None));

        Assert.Equal("antoine.b@orange.fr", candidate.Value);
        Assert.Equal(CandidateKind.Email, candidate.Kind);
        Assert.Equal("Email", candidate.Label);
        Assert.Equal(FillHistory.SourceName, candidate.SourceName);
        Assert.True(candidate.IsFromHistory);
    }

    [Fact]
    public async Task RecordAsync_CountsUsesPerKindOfField()
    {
        await RecordAsync("Email", "antoine.b@orange.fr");
        await RecordAsync("Adresse e-mail", "antoine.b@orange.fr");

        var entries = await _history.GetEntriesAsync(CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(1, entry.UseCount));
    }

    [Fact]
    public async Task RecordAsync_IgnoresCaseAndAccentsOfTheLabel()
    {
        await RecordAsync("Téléphone", "06 47 21 85 39");
        await RecordAsync("TELEPHONE", "06 47 21 85 39");

        Assert.Equal(2, Assert.Single(await _history.GetEntriesAsync(CancellationToken.None)).UseCount);
    }

    [Theory]
    [InlineData("Prénom", "Antoine", CandidateKind.FirstName)]
    [InlineData("Nom", "Barthélémy", CandidateKind.LastName)]
    [InlineData("Nom", "Antoine Barthélémy", CandidateKind.FullName)]
    [InlineData("Nom", "Wei Zhang", CandidateKind.Text)]
    [InlineData("Email ou nom d'utilisateur", "antoine51", CandidateKind.Text)]
    [InlineData("Ville", "Taissy", CandidateKind.City)]
    [InlineData("Téléphone", "06 47 21 85 39", CandidateKind.Phone)]
    [InlineData("Numéro adhérent", "A-4521", CandidateKind.Reference)]
    [InlineData("Commentaire", "RAS", CandidateKind.Text)]
    public async Task RecordAsync_KeepsTheShapeOfTheValueOrWhatTheFieldExpected(string label, string value, CandidateKind expected)
    {
        await RecordAsync(label, value);

        Assert.Equal(expected, Assert.Single(await _history.GetEntriesAsync(CancellationToken.None)).Kind);
    }

    [Fact]
    public async Task RecordAsync_NeverLearnsPasswordsOrEmptyValues()
    {
        await _history.RecordAsync(
            new[]
            {
                new WrittenField { Field = TestFields.Password("password", "Mot de passe"), Value = "secret" },
                new WrittenField { Field = TestFields.Text("misc", "Commentaire"), Value = "   " },
                new WrittenField { Field = TestFields.Text("long", "Texte"), Value = new string('a', 300) },
            },
            CancellationToken.None);

        Assert.Empty(await _history.GetEntriesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordAsync_KeepsTheMostRecentlyUsedValuesOnly()
    {
        for (var index = 0; index <= FillHistory.MaxEntries; index++)
        {
            _clock.Now = _clock.Now.AddMinutes(1);
            await RecordAsync("Référence", $"REF-{index}");
        }

        var entries = await _history.GetEntriesAsync(CancellationToken.None);

        Assert.Equal(FillHistory.MaxEntries, entries.Count);
        Assert.DoesNotContain(entries, entry => entry.Value == "REF-0");
        Assert.Equal($"REF-{FillHistory.MaxEntries}", entries[0].Value);
    }

    [Fact]
    public async Task ForgetAsync_TakesBackOneUseSoAValueUsedTwiceIsNoLongerOffered()
    {
        await RecordAsync("Email", "antoine.b@orange.fr");
        await RecordAsync("Email", "antoine.b@orange.fr");

        await _history.ForgetAsync(new[] { Written("Email", "antoine.b@orange.fr") }, CancellationToken.None);

        Assert.Equal(1, Assert.Single(await _history.GetEntriesAsync(CancellationToken.None)).UseCount);
        Assert.Empty(await _history.GetCandidatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ForgetAsync_KeepsAValueUsedOftenOfferedAfterOneUndo()
    {
        for (var use = 0; use < 15; use++)
        {
            await RecordAsync("Email", "antoine.b@orange.fr");
        }

        await _history.ForgetAsync(new[] { Written("Email", "antoine.b@orange.fr") }, CancellationToken.None);

        Assert.Single(await _history.GetCandidatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ForgetAsync_RemovesAValueUsedOnce()
    {
        await RecordAsync("Email", "antoine.b@orange.fr");

        await _history.ForgetAsync(new[] { Written("Email", "antoine.b@orange.fr") }, CancellationToken.None);

        Assert.Empty(await _history.GetEntriesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("Numéro de carte", "4970 1012 3456 7890")]
    [InlineData("Cryptogramme visuel", "123")]
    [InlineData("Code de vérification", "482913")]
    [InlineData("Numéro de sécurité sociale", "1 90 03 51 454 123 45")]
    [InlineData("IBAN", "FR76 3000 6000 0112 3456 7890 189")]
    [InlineData("Commentaire", "4970101234567890")]
    [InlineData("Commentaire", "FR76 3000 6000 0112 3456 7890 189")]
    [InlineData("Numéro de passeport", "18AB12345")]
    [InlineData("N° de permis de conduire", "130751234567")]
    [InlineData("Remarque", "2 85 11 75 116 007 02")]
    public async Task RecordAsync_NeverLearnsSensitiveValues(string label, string value)
    {
        await RecordAsync(label, value);

        Assert.Empty(await _history.GetEntriesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordAsync_NeverLearnsCodesOfFieldsNamedTechnically()
    {
        await _history.RecordAsync(
            new[]
            {
                new WrittenField { Field = TestFields.Text("otp", string.Empty) with { TechnicalName = "oneTimeCode" }, Value = "482913" },
                new WrittenField { Field = TestFields.Text("cc", string.Empty) with { TechnicalName = "cc-csc" }, Value = "123" },
            },
            CancellationToken.None);

        Assert.Empty(await _history.GetEntriesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ClearAsync_ForgetsEverythingAndSavesIt()
    {
        await RecordAsync("Email", "antoine.b@orange.fr");

        await _history.ClearAsync(CancellationToken.None);

        Assert.Empty(await _history.GetEntriesAsync(CancellationToken.None));
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task IsEnabled_WhenOff_LearnsAndOffersNothingButKeepsWhatWasLearned()
    {
        await RecordAsync("Email", "antoine.b@orange.fr");
        await RecordAsync("Email", "antoine.b@orange.fr");

        _history.IsEnabled = false;
        await RecordAsync("Ville", "Taissy");

        Assert.Empty(await _history.GetCandidatesAsync(CancellationToken.None));
        Assert.Single(await _history.GetEntriesAsync(CancellationToken.None));
        _history.IsEnabled = true;
        Assert.Single(await _history.GetCandidatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task History_IsLoadedOnceAndSavedAfterEachChange()
    {
        _store.Preload(new HistoryEntry { FieldLabel = "Email", Value = "antoine.b@orange.fr", Kind = CandidateKind.Email, UseCount = 3, LastUsed = _clock.Now });

        await RecordAsync("Ville", "Taissy");
        _ = await _history.GetCandidatesAsync(CancellationToken.None);

        Assert.Equal(1, _store.LoadCount);
        Assert.Equal(2, _store.Saved.Count);
    }

    [Fact]
    public async Task History_StillWorksWhenItCannotBeSaved()
    {
        _store.FailsToSave = true;

        await RecordAsync("Email", "antoine.b@orange.fr");
        await RecordAsync("Email", "antoine.b@orange.fr");

        Assert.Single(await _history.GetCandidatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Observe_LearnsFromFillsAndPicksAndForgetsWhatIsUndone()
    {
        var memory = new FillMemory();
        _history.Observe(memory);

        memory.Remember(new[] { Written("Email", "antoine.b@orange.fr") });
        memory.Append(Written("Email", "antoine.b@orange.fr"));
        Assert.Single(await _history.GetCandidatesAsync(CancellationToken.None));

        memory.ReportUndone(new[] { Written("Email", "antoine.b@orange.fr") });
        Assert.Empty(await _history.GetCandidatesAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        _history.Dispose();
    }

    private static WrittenField Written(string label, string value)
    {
        return new WrittenField { Field = TestFields.Text(label.ToLowerInvariant(), label), Value = value };
    }

    private Task RecordAsync(string label, string value)
    {
        return _history.RecordAsync(new[] { Written(label, value) }, CancellationToken.None);
    }
}
