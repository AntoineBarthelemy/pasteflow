using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
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
    public async Task ForgetAsync_RemovesAValueUndone()
    {
        await RecordAsync("Email", "antoine.b@orange.fr");
        await RecordAsync("Email", "antoine.b@orange.fr");

        await _history.ForgetAsync(new[] { Written("Email", "antoine.b@orange.fr") }, CancellationToken.None);

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
        Assert.Empty(await _history.GetEntriesAsync(CancellationToken.None));
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
