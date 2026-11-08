using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.History;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Security;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Profile;

/// <summary>
/// Completes the profile of the user with what they fill, and counts the uses of its values.
/// </summary>
/// <remarks>
/// <para>A value enters the profile only when the user wrote or kept it themselves, into a field about them ("Votre
/// e-mail", or a page "Créer un compte"), <see cref="UsesToLearn"/> times, without undoing it. What is merely shown
/// on screen is never learned. The user is told, with a way to take it back.</para>
/// <para>A sensitive value, such as an IBAN, is only added once the user agreed and proved it is them; until then it
/// is only counted in memory, never saved.</para>
/// <para>Each use of a value already in the profile is counted, with the site where it was written, so that among
/// several values PasteFlow prefers the one used on the site, or else the most used one.</para>
/// </remarks>
public sealed class ProfileLearner : IDisposable
{
    /// <summary>
    /// The number of times a value must be written before it enters the profile.
    /// </summary>
    public const int UsesToLearn = 2;

    /// <summary>
    /// The maximum number of values counted while waiting to be learned; the oldest ones are forgotten first.
    /// </summary>
    public const int MaxPending = 200;

    private const string AddSensitiveReason = "Ajouter une info sensible à ta fiche PasteFlow";

    private readonly ProfileBook _book;
    private readonly IProfileStore _pendingStore;
    private readonly SensitiveGate _gate;
    private readonly IProfileLearningPrompts _prompts;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<(ProfileTopic Topic, string Value), int> _sensitiveSeen = new();
    private List<ProfileValue>? _pending;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileLearner"/> class.
    /// </summary>
    /// <param name="book">The profile completed.</param>
    /// <param name="pendingStore">Keeps the values counted while waiting to be learned.</param>
    /// <param name="gate">The gate that guards the sensitive values.</param>
    /// <param name="prompts">Tells the user what was learned and asks before keeping a sensitive value.</param>
    public ProfileLearner(ProfileBook book, IProfileStore pendingStore, SensitiveGate gate, IProfileLearningPrompts prompts)
        : this(book, pendingStore, gate, prompts, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileLearner"/> class with a given clock.
    /// </summary>
    /// <param name="book">The profile completed.</param>
    /// <param name="pendingStore">Keeps the values counted while waiting to be learned.</param>
    /// <param name="gate">The gate that guards the sensitive values.</param>
    /// <param name="prompts">Tells the user what was learned and asks before keeping a sensitive value.</param>
    /// <param name="timeProvider">The clock used to date the values counted.</param>
    public ProfileLearner(ProfileBook book, IProfileStore pendingStore, SensitiveGate gate, IProfileLearningPrompts prompts, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(pendingStore);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _book = book;
        _pendingStore = pendingStore;
        _gate = gate;
        _prompts = prompts;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the profile learns from the fills; the uses are not counted either when off.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Tells which topic of the profile a field asks for, judging by its label and the value written.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The value written into it.</param>
    /// <returns>The topic, or <see langword="null"/> when the field asks for nothing the profile keeps.</returns>
    public static ProfileTopic? TopicOf(FormField field, string value)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);

        var clues = FieldClues.Of(field);
        if (clues.IsConfirmation)
        {
            // A confirmation repeats the value of another field, which is learned there
            return null;
        }

        var kinds = clues.ExpectedKinds;
        return true switch
        {
            _ when kinds.Contains(CandidateKind.Iban) => (ProfileTopic?)ProfileTopic.Iban,
            _ when clues.Mentions(LabelConcepts.SocialSecurity) => ProfileTopic.SocialSecurityNumber,
            _ when clues.Mentions("passeport") || clues.Mentions("passport") => ProfileTopic.PassportNumber,
            _ when clues.Mentions("permis") => ProfileTopic.DrivingLicenceNumber,
            _ when clues.Mentions("identite") && clues.Mentions("carte") => ProfileTopic.IdentityCardNumber,
            _ when clues.Mentions("bic") => ProfileTopic.Bic,
            _ when kinds.Contains(CandidateKind.Email) => ProfileTopic.Email,
            _ when kinds.Contains(CandidateKind.Phone) => ProfileTopic.Phone,
            _ when kinds.Contains(CandidateKind.Url) => value.Contains("linkedin.", StringComparison.OrdinalIgnoreCase) ? ProfileTopic.LinkedIn : ProfileTopic.Website,
            _ when kinds.Contains(CandidateKind.Date) => clues.Mentions(LabelConcepts.Birth) ? ProfileTopic.BirthDate : (ProfileTopic?)null,
            _ when clues.Mentions(LabelConcepts.PostalCode) => ProfileTopic.PostalCode,
            _ when clues.Mentions(LabelConcepts.FirstName) => ProfileTopic.FirstName,
            _ when clues.Mentions(LabelConcepts.Name) => value.Trim().Contains(' ', StringComparison.Ordinal) ? (ProfileTopic?)null : ProfileTopic.LastName,
            _ when clues.Mentions(LabelConcepts.City) => ProfileTopic.City,
            _ when clues.Mentions(LabelConcepts.Country) => ProfileTopic.Country,
            _ when clues.Mentions(LabelConcepts.Company) => ProfileTopic.Company,
            _ when clues.Mentions(LabelConcepts.School) => ProfileTopic.School,
            _ when clues.Mentions(LabelConcepts.JobTitle) => ProfileTopic.JobTitle,
            _ when clues.Mentions(LabelConcepts.Address) => ProfileTopic.Street,
            _ => null,
        };
    }

    /// <summary>
    /// Learns from what the history records (fills, suggestions picked, values typed), from the sensitive values
    /// written, and takes back what is undone.
    /// </summary>
    /// <param name="history">The history of fills, which records the ordinary values.</param>
    /// <param name="fillMemory">The memory of the fills, which also sees the sensitive values written.</param>
    public void Observe(FillHistory history, FillMemory fillMemory)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(fillMemory);

        history.Recorded += async (_, written) => await LearnAsync(written, CancellationToken.None);
        fillMemory.Filled += async (_, written) => await LearnSensitiveAsync(written, CancellationToken.None);
        fillMemory.Undone += async (_, written) => await ForgetAsync(written, CancellationToken.None);
    }

    /// <summary>
    /// Learns from ordinary values written into fields; sensitive ones are left to <see cref="LearnSensitiveAsync"/>.
    /// </summary>
    /// <param name="writtenFields">The values and the fields they were written into.</param>
    /// <param name="cancellationToken">A token to cancel the learning.</param>
    /// <returns>A task completed once the values are counted.</returns>
    public async Task LearnAsync(IEnumerable<WrittenField> writtenFields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writtenFields);

        foreach (var written in writtenFields.Where(written => IsEnabled && !IsSensitive(written)))
        {
            if (await LearnableAsync(written, cancellationToken) is not { } learnable)
            {
                continue;
            }

            var count = await CountPendingAsync(learnable.Topic, learnable.Value, cancellationToken);
            if (count >= UsesToLearn)
            {
                await AddLearnedAsync(learnable.Topic, learnable.Value, written.Page.Site, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Learns from the sensitive values written into fields: after <see cref="UsesToLearn"/> uses, the user is asked,
    /// then Windows Hello, before the value enters the profile.
    /// </summary>
    /// <param name="writtenFields">The values and the fields they were written into.</param>
    /// <param name="cancellationToken">A token to cancel the learning.</param>
    /// <returns>A task completed once the values are counted, and added if the user agreed.</returns>
    public async Task LearnSensitiveAsync(IEnumerable<WrittenField> writtenFields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writtenFields);

        foreach (var written in writtenFields.Where(written => IsEnabled && IsSensitive(written)))
        {
            if (await LearnableAsync(written, cancellationToken) is not { } learnable)
            {
                continue;
            }

            // Only counted in memory: a sensitive value is never saved without the user's agreement
            var key = (learnable.Topic, SensitiveKey(learnable.Value));
            var count = _sensitiveSeen.GetValueOrDefault(key) + 1;
            if (count < UsesToLearn)
            {
                _sensitiveSeen[key] = count;
                continue;
            }

            _ = _sensitiveSeen.Remove(key);
            await AddSensitiveAsync(learnable.Topic, learnable.Value, ProfileOrigin.Learned, cancellationToken);
        }
    }

    /// <summary>
    /// Takes back one use of values the user undid.
    /// </summary>
    /// <param name="writtenFields">The values undone.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>A task completed once the uses are taken back.</returns>
    public async Task ForgetAsync(IEnumerable<WrittenField> writtenFields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writtenFields);

        foreach (var written in writtenFields)
        {
            var value = written.Value.Trim();
            if (TopicOf(written.Field, value) is not { } topic)
            {
                continue;
            }

            var key = (topic, SensitiveKey(value));
            if (_sensitiveSeen.TryGetValue(key, out var seen))
            {
                if (seen <= 1)
                {
                    _ = _sensitiveSeen.Remove(key);
                }
                else
                {
                    _sensitiveSeen[key] = seen - 1;
                }
            }

            await ChangePendingAsync(
                pending =>
                {
                    var index = pending.FindIndex(item => item.Topic == topic && ProfileValueRules.AreSame(topic, item.Value, value));
                    if (index >= 0)
                    {
                        if (pending[index].UseCount <= 1)
                        {
                            pending.RemoveAt(index);
                        }
                        else
                        {
                            pending[index] = pending[index] with { UseCount = pending[index].UseCount - 1 };
                        }
                    }

                    return 0;
                },
                cancellationToken);
        }
    }

    /// <summary>
    /// Adds the name and the email of the Windows account to the profile, so that it is useful from the first launch.
    /// </summary>
    /// <param name="account">What Windows knows about its user.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>The number of values added.</returns>
    public async Task<int> ImportWindowsAccountAsync(WindowsAccount account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        var added = 0;
        foreach (var (topic, value) in new[] { (ProfileTopic.FirstName, account.FirstName), (ProfileTopic.LastName, account.LastName), (ProfileTopic.Email, account.Email) })
        {
            if (value.Trim().Length > 0 && IsAdded(await _book.AddAsync(topic, value, string.Empty, ProfileOrigin.WindowsAccount, cancellationToken)))
            {
                added++;
            }
        }

        return added;
    }

    /// <summary>
    /// Adds the values of an identity kept elsewhere, such as in the password manager: ordinary values at once,
    /// sensitive ones only once the user agreed and proved it is them.
    /// </summary>
    /// <param name="identity">The values of the identity.</param>
    /// <param name="origin">Where the values come from.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>The number of values added.</returns>
    public async Task<int> ImportIdentityAsync(IEnumerable<ProfileValue> identity, ProfileOrigin origin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var added = 0;
        foreach (var value in identity)
        {
            var isAdded = value.IsSensitive
                ? await AddSensitiveAsync(value.Topic, value.Value, origin, cancellationToken)
                : IsAdded(await _book.AddAsync(value.Topic, value.Value, value.Label, origin, cancellationToken));
            if (isAdded)
            {
                added++;
            }
        }

        return added;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lock.Dispose();
    }

    private static bool IsSensitive(WrittenField written)
    {
        return SensitiveFields.IsSensitive(written.Field, written.Value) || SensitiveFormats.IsSensitiveValue(written.Value);
    }

    private static bool IsAdded(ProfileEditResult result)
    {
        return result is ProfileEditResult.Done or ProfileEditResult.NotSaved;
    }

    private static string SensitiveKey(string value)
    {
        return SensitiveFormats.Compact(value).ToUpperInvariant();
    }

    /// <summary>
    /// Tells whether a value written can teach the profile something: counts its use if it is already there, and
    /// gives its topic if it is new and written into a field about the user.
    /// </summary>
    /// <param name="written">The value and its field.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The topic and the value to count, or <see langword="null"/> if there is nothing more to learn.</returns>
    private async Task<(ProfileTopic Topic, string Value)?> LearnableAsync(WrittenField written, CancellationToken cancellationToken)
    {
        var value = written.Value.Trim();
        if (TopicOf(written.Field, value) is not { } topic || ProfileValueRules.Check(topic, value) != ProfileEditResult.Done)
        {
            return null;
        }

        var owner = FieldOwnership.Of(written.Field, written.Page);
        if (owner == FieldOwner.SomeoneElse)
        {
            return null;
        }

        var values = await _book.GetValuesAsync(cancellationToken);
        var known = values.FirstOrDefault(item => item.Topic == topic && ProfileValueRules.AreSame(topic, item.Value, value));
        if (known is not null)
        {
            _ = await _book.RecordUseAsync(known.Id, written.Page.Site, cancellationToken);
            return null;
        }

        // A new value is only learned from a field known to be the user's
        return owner == FieldOwner.User ? (topic, value) : null;
    }

    private Task<int> CountPendingAsync(ProfileTopic topic, string value, CancellationToken cancellationToken)
    {
        return ChangePendingAsync(
            pending =>
            {
                var now = _timeProvider.GetUtcNow();
                var index = pending.FindIndex(item => item.Topic == topic && ProfileValueRules.AreSame(topic, item.Value, value));
                var counted = index < 0
                    ? new ProfileValue { Topic = topic, Value = value, Origin = ProfileOrigin.Learned, UseCount = 1, AddedAt = now }
                    : pending[index] with { UseCount = pending[index].UseCount + 1, AddedAt = now };
                if (index >= 0)
                {
                    pending.RemoveAt(index);
                }

                // Learned now, it no longer waits
                if (counted.UseCount < UsesToLearn)
                {
                    pending.Add(counted);
                }

                if (pending.Count > MaxPending)
                {
                    pending.Sort((first, second) => second.AddedAt.CompareTo(first.AddedAt));
                    pending.RemoveRange(MaxPending, pending.Count - MaxPending);
                }

                return counted.UseCount;
            },
            cancellationToken);
    }

    private async Task AddLearnedAsync(ProfileTopic topic, string value, string site, CancellationToken cancellationToken)
    {
        if (!IsAdded(await _book.AddAsync(topic, value, string.Empty, ProfileOrigin.Learned, cancellationToken)))
        {
            return;
        }

        var values = await _book.GetValuesAsync(cancellationToken);
        if (values.FirstOrDefault(item => item.Topic == topic && ProfileValueRules.AreSame(topic, item.Value, value)) is { } added)
        {
            _ = await _book.RecordUseAsync(added.Id, site, cancellationToken);
            _prompts.ShowAdded(added, () => _book.RemoveAsync(added.Id, CancellationToken.None));
        }
    }

    private async Task<bool> AddSensitiveAsync(ProfileTopic topic, string value, ProfileOrigin origin, CancellationToken cancellationToken)
    {
        var shown = new ProfileValue { Topic = topic, Value = value, Origin = origin };
        if (ProfileValueRules.Check(topic, value) != ProfileEditResult.Done
            || !await _prompts.AskToAddSensitiveAsync(shown, cancellationToken)
            || await _gate.OpenAsync(AddSensitiveReason, cancellationToken) != PresenceResult.Verified)
        {
            return false;
        }

        return IsAdded(await _book.AddAsync(topic, value, string.Empty, origin, cancellationToken));
    }

    private async Task<int> ChangePendingAsync(Func<List<ProfileValue>, int> change, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _pending ??= (await _pendingStore.LoadAsync(cancellationToken)).ToList();
            var result = change(_pending);

            // Best effort: if saving fails, the counts still work until the application closes
            _ = await _pendingStore.TrySaveAsync(_pending.ToArray(), cancellationToken);
            return result;
        }
        finally
        {
            _ = _lock.Release();
        }
    }
}
