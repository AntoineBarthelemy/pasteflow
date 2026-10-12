using PasteFlow.Core.Profile;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Profile;

public sealed class ProfileBookTests : IDisposable
{
    private readonly FakeProfileStore _store = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly ProfileBook _book;

    public ProfileBookTests()
    {
        _book = new ProfileBook(_store, _clock);
    }

    [Fact]
    public async Task AddAsync_KeepsAndSavesTheValue()
    {
        var result = await AddAsync(ProfileTopic.Email, "  antoine.b@orange.fr ", "Perso");

        Assert.Equal(ProfileEditResult.Done, result);
        var value = Assert.Single(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Equal("antoine.b@orange.fr", value.Value);
        Assert.Equal("Perso", value.Label);
        Assert.Equal(ProfileOrigin.Manual, value.Origin);
        Assert.Equal(_clock.Now, value.AddedAt);
        Assert.Equal(value, Assert.Single(_store.Saved));
    }

    [Fact]
    public async Task AddAsync_KeepsSeveralValuesOfATopic()
    {
        await AddAsync(ProfileTopic.Email, "antoine.b@orange.fr");
        await AddAsync(ProfileTopic.Email, "antoine.barthelemy@etudiant.univ-reims.fr");

        Assert.Equal(2, (await _book.GetValuesAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task AddAsync_NeverAddsTheSameValueTwice()
    {
        await AddAsync(ProfileTopic.Iban, "FR76 3000 6000 0112 3456 7890 189");

        var result = await AddAsync(ProfileTopic.Iban, "FR7630006000011234567890189");

        Assert.Equal(ProfileEditResult.AlreadyThere, result);
        Assert.Single(await _book.GetValuesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AddAsync_KeepsTheSameTextUnderTwoTopics()
    {
        await AddAsync(ProfileTopic.City, "Reims");

        Assert.Equal(ProfileEditResult.Done, await AddAsync(ProfileTopic.School, "Reims"));
    }

    [Theory]
    [InlineData(ProfileTopic.Email, "antoine.orange.fr", ProfileEditResult.InvalidFormat)]
    [InlineData(ProfileTopic.Other, "4111 1111 1111 1111", ProfileEditResult.PaymentCardRefused)]
    [InlineData(ProfileTopic.City, " ", ProfileEditResult.Empty)]
    public async Task AddAsync_RefusesValuesBreakingTheRules(ProfileTopic topic, string value, ProfileEditResult expected)
    {
        Assert.Equal(expected, await AddAsync(topic, value));
        Assert.Empty(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task AddAsync_WhenFull_AddsNothing()
    {
        _store.Preload(Enumerable.Range(0, ProfileBook.MaxValues)
            .Select(index => new ProfileValue { Topic = ProfileTopic.Other, Value = $"valeur {index}" })
            .ToArray());

        Assert.Equal(ProfileEditResult.Full, await AddAsync(ProfileTopic.City, "Taissy"));
    }

    [Fact]
    public async Task AddAsync_WhenTheStoreFails_KeepsTheValueAndSaysItIsNotSaved()
    {
        _store.FailsToSave = true;

        Assert.Equal(ProfileEditResult.NotSaved, await AddAsync(ProfileTopic.City, "Taissy"));
        Assert.Single(await _book.GetValuesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetValuesAsync_LoadsOnceAndGroupsByTopic()
    {
        _store.Preload(
            new ProfileValue { Topic = ProfileTopic.City, Value = "Taissy", AddedAt = _clock.Now },
            new ProfileValue { Topic = ProfileTopic.Email, Value = "b@orange.fr", AddedAt = _clock.Now.AddDays(1) },
            new ProfileValue { Topic = ProfileTopic.Email, Value = "a@orange.fr", AddedAt = _clock.Now });

        var values = await _book.GetValuesAsync(CancellationToken.None);
        _ = await _book.GetValuesAsync(CancellationToken.None);

        Assert.Equal(new[] { "a@orange.fr", "b@orange.fr", "Taissy" }, values.Select(value => value.Value));
        Assert.Equal(1, _store.LoadCount);
    }

    [Fact]
    public async Task CorrectAsync_ChangesTheValueAndMakesItManual()
    {
        var learned = new ProfileValue { Topic = ProfileTopic.City, Value = "Tassy", Origin = ProfileOrigin.Learned };
        _store.Preload(learned);

        var result = await _book.CorrectAsync(learned.Id, ProfileTopic.City, "Taissy", string.Empty, CancellationToken.None);

        Assert.Equal(ProfileEditResult.Done, result);
        var value = Assert.Single(await _book.GetValuesAsync(CancellationToken.None));
        Assert.Equal(learned.Id, value.Id);
        Assert.Equal("Taissy", value.Value);
        Assert.Equal(ProfileOrigin.Manual, value.Origin);
    }

    [Fact]
    public async Task CorrectAsync_RefusesToDuplicateAnotherValue()
    {
        var first = new ProfileValue { Topic = ProfileTopic.Email, Value = "a@orange.fr" };
        var second = new ProfileValue { Topic = ProfileTopic.Email, Value = "b@orange.fr" };
        _store.Preload(first, second);

        Assert.Equal(ProfileEditResult.AlreadyThere, await _book.CorrectAsync(second.Id, ProfileTopic.Email, "A@orange.fr", string.Empty, CancellationToken.None));
        Assert.Equal(ProfileEditResult.Done, await _book.CorrectAsync(second.Id, ProfileTopic.Email, "b@orange.fr", "Pro", CancellationToken.None));
    }

    [Fact]
    public async Task CorrectAsync_ChecksTheNewValue()
    {
        var value = new ProfileValue { Topic = ProfileTopic.Email, Value = "a@orange.fr" };
        _store.Preload(value);

        Assert.Equal(ProfileEditResult.InvalidFormat, await _book.CorrectAsync(value.Id, ProfileTopic.Email, "a.orange.fr", string.Empty, CancellationToken.None));
        Assert.Equal(ProfileEditResult.NotFound, await _book.CorrectAsync(Guid.NewGuid(), ProfileTopic.Email, "c@orange.fr", string.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_RemovesAndSaves()
    {
        var value = new ProfileValue { Topic = ProfileTopic.City, Value = "Taissy" };
        _store.Preload(value);

        Assert.Equal(ProfileEditResult.Done, await _book.RemoveAsync(value.Id, CancellationToken.None));
        Assert.Empty(_store.Saved);
        Assert.Equal(ProfileEditResult.NotFound, await _book.RemoveAsync(value.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_AddsOnlyNewAndValidValues()
    {
        await AddAsync(ProfileTopic.Email, "a@orange.fr");
        var imported = new[]
        {
            new ProfileValue { Topic = ProfileTopic.Email, Value = "A@orange.fr" },
            new ProfileValue { Topic = ProfileTopic.City, Value = "Taissy", Label = "Maison" },
            new ProfileValue { Topic = ProfileTopic.Iban, Value = "FR76 3000 6000 0112 3456 7890 188" },
            new ProfileValue { Topic = ProfileTopic.Other, Value = "4111 1111 1111 1111" },
        };

        var summary = await _book.ImportAsync(imported, CancellationToken.None);

        Assert.Equal(1, summary.Added);
        Assert.True(summary.IsSaved);
        var city = Assert.Single(await _book.GetValuesAsync(CancellationToken.None), value => value.Topic == ProfileTopic.City);
        Assert.Equal("Maison", city.Label);
        Assert.Equal(ProfileOrigin.Imported, city.Origin);
        Assert.NotEqual(imported[1].Id, city.Id);
    }

    [Fact]
    public async Task ImportAsync_WithNothingNew_ChangesNothing()
    {
        await AddAsync(ProfileTopic.City, "Taissy");

        var summary = await _book.ImportAsync(new[] { new ProfileValue { Topic = ProfileTopic.City, Value = "taissy" } }, CancellationToken.None);

        Assert.Equal(0, summary.Added);
        Assert.True(summary.IsSaved);
    }

    public void Dispose()
    {
        _book.Dispose();
    }

    private Task<ProfileEditResult> AddAsync(ProfileTopic topic, string value, string label = "")
    {
        return _book.AddAsync(topic, value, label, ProfileOrigin.Manual, CancellationToken.None);
    }
}
