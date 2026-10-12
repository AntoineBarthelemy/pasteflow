namespace PasteFlow.Core.Profile;

/// <summary>
/// The profile of the user: the informations about them that PasteFlow can write into forms, kept by an
/// <see cref="IProfileStore"/> which encrypts them.
/// </summary>
/// <remarks>
/// <para>Every value is checked before it enters (<see cref="ProfileValueRules"/>): an IBAN with a wrong key or an
/// email without "@" is refused, and payment card numbers are never kept.</para>
/// <para>A topic can have several values, but never twice the same one.</para>
/// </remarks>
public sealed class ProfileBook : IDisposable
{
    /// <summary>
    /// The maximum number of values kept.
    /// </summary>
    public const int MaxValues = 200;

    private readonly IProfileStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<ProfileValue>? _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBook"/> class.
    /// </summary>
    /// <param name="store">The component that keeps the profile between two launches.</param>
    public ProfileBook(IProfileStore store)
        : this(store, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBook"/> class with a given clock.
    /// </summary>
    /// <param name="store">The component that keeps the profile between two launches.</param>
    /// <param name="timeProvider">The clock used to date the values added.</param>
    public ProfileBook(IProfileStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets every value of the profile, grouped by topic in the order shown to the user, the oldest first in each topic.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The values of the profile.</returns>
    public async Task<IReadOnlyList<ProfileValue>> GetValuesAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var values = await LoadedValuesAsync(cancellationToken);
            return values.OrderBy(value => value.Topic).ThenBy(value => value.AddedAt).ToList();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>
    /// Adds a value to the profile.
    /// </summary>
    /// <param name="topic">What the value is about.</param>
    /// <param name="value">The value.</param>
    /// <param name="label">The name the user gives to the value, or an empty string.</param>
    /// <param name="origin">Where the value comes from.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>What became of the change.</returns>
    public async Task<ProfileEditResult> AddAsync(ProfileTopic topic, string value, string label, ProfileOrigin origin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(label);

        var check = ProfileValueRules.Check(topic, value);
        if (check != ProfileEditResult.Done)
        {
            return check;
        }

        return await ChangeAsync(
            values =>
            {
                if (values.Exists(existing => existing.Topic == topic && ProfileValueRules.AreSame(topic, existing.Value, value)))
                {
                    return ProfileEditResult.AlreadyThere;
                }

                if (values.Count >= MaxValues)
                {
                    return ProfileEditResult.Full;
                }

                values.Add(new ProfileValue
                {
                    Topic = topic,
                    Value = value.Trim(),
                    Label = label.Trim(),
                    Origin = origin,
                    AddedAt = _timeProvider.GetUtcNow(),
                });
                return ProfileEditResult.Done;
            },
            cancellationToken);
    }

    /// <summary>
    /// Corrects a value of the profile: its topic, the value itself or its name.
    /// </summary>
    /// <param name="id">The identifier of the value to correct.</param>
    /// <param name="topic">The new topic.</param>
    /// <param name="value">The new value.</param>
    /// <param name="label">The new name, or an empty string.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>What became of the change.</returns>
    public async Task<ProfileEditResult> CorrectAsync(Guid id, ProfileTopic topic, string value, string label, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(label);

        var check = ProfileValueRules.Check(topic, value);
        if (check != ProfileEditResult.Done)
        {
            return check;
        }

        return await ChangeAsync(
            values =>
            {
                var index = values.FindIndex(existing => existing.Id == id);
                if (index < 0)
                {
                    return ProfileEditResult.NotFound;
                }

                if (values.Exists(existing => existing.Id != id && existing.Topic == topic && ProfileValueRules.AreSame(topic, existing.Value, value)))
                {
                    return ProfileEditResult.AlreadyThere;
                }

                // A value corrected by the user becomes theirs, wherever it came from
                values[index] = values[index] with { Topic = topic, Value = value.Trim(), Label = label.Trim(), Origin = ProfileOrigin.Manual };
                return ProfileEditResult.Done;
            },
            cancellationToken);
    }

    /// <summary>
    /// Counts a use of a value of the profile, written into a field, and the site where it was written: among several
    /// values of a topic, PasteFlow then prefers the one used on this site, or else the most used one.
    /// </summary>
    /// <param name="id">The identifier of the value used.</param>
    /// <param name="site">The site where it was written, such as "univ-reims.fr", or an empty string.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>What became of the change.</returns>
    public Task<ProfileEditResult> RecordUseAsync(Guid id, string site, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(site);

        return ChangeAsync(
            values =>
            {
                var index = values.FindIndex(existing => existing.Id == id);
                if (index < 0)
                {
                    return ProfileEditResult.NotFound;
                }

                values[index] = values[index] with { UseCount = values[index].UseCount + 1, Sites = SiteUses.Add(values[index].Sites, site) };
                return ProfileEditResult.Done;
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes a value from the profile.
    /// </summary>
    /// <param name="id">The identifier of the value to remove.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>What became of the change.</returns>
    public Task<ProfileEditResult> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        return ChangeAsync(
            values => values.RemoveAll(existing => existing.Id == id) > 0 ? ProfileEditResult.Done : ProfileEditResult.NotFound,
            cancellationToken);
    }

    /// <summary>
    /// Adds the values of an export of the profile. Values already in the profile, and values that do not follow
    /// the rules of the profile, are left aside.
    /// </summary>
    /// <param name="imported">The values of the export.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>The number of values added, and whether they could be saved.</returns>
    public async Task<ProfileImportSummary> ImportAsync(IEnumerable<ProfileValue> imported, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imported);

        var candidates = imported.Where(value => ProfileValueRules.Check(value.Topic, value.Value) == ProfileEditResult.Done).ToList();
        var added = 0;
        var result = await ChangeAsync(
            values =>
            {
                foreach (var candidate in candidates)
                {
                    var isKnown = values.Exists(existing => existing.Topic == candidate.Topic && ProfileValueRules.AreSame(candidate.Topic, existing.Value, candidate.Value));
                    if (isKnown || values.Count >= MaxValues)
                    {
                        continue;
                    }

                    values.Add(candidate with
                    {
                        Id = Guid.NewGuid(),
                        Value = candidate.Value.Trim(),
                        Label = candidate.Label.Trim(),
                        Origin = ProfileOrigin.Imported,
                        AddedAt = _timeProvider.GetUtcNow(),
                    });
                    added++;
                }

                return added > 0 ? ProfileEditResult.Done : ProfileEditResult.AlreadyThere;
            },
            cancellationToken);

        return new ProfileImportSummary { Added = added, IsSaved = result != ProfileEditResult.NotSaved };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task<ProfileEditResult> ChangeAsync(Func<List<ProfileValue>, ProfileEditResult> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var values = await LoadedValuesAsync(cancellationToken);
            var result = change(values);
            if (result != ProfileEditResult.Done)
            {
                return result;
            }

            // Not saved, the change still works until the application closes, and the user is told
            return await _store.TrySaveAsync(values.ToArray(), cancellationToken) ? ProfileEditResult.Done : ProfileEditResult.NotSaved;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async Task<List<ProfileValue>> LoadedValuesAsync(CancellationToken cancellationToken)
    {
        _values ??= (await _store.LoadAsync(cancellationToken)).ToList();
        return _values;
    }
}
