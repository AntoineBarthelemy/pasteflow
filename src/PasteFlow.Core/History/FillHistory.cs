using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.History;

/// <summary>
/// Learns the values the user writes into each kind of field, and offers them again for fields the screen says
/// nothing about.
/// </summary>
/// <remarks>
/// <para>Learning is cautious: a value is offered only once it was written at least <see cref="MinUses"/> times into the
/// same kind of field, by a fill, a suggestion picked or the user typing it. A value undone is forgotten.</para>
/// <para>Passwords, card numbers, security codes and other sensitive values are never learned
/// (see <see cref="SensitiveFields"/>). The history is kept by an <see cref="IHistoryStore"/>, which encrypts it.</para>
/// </remarks>
public sealed class FillHistory : ICandidateSource, IDisposable
{
    /// <summary>
    /// The number of uses after which a value is offered again.
    /// </summary>
    public const int MinUses = 2;

    /// <summary>
    /// The maximum number of values kept; the least recently used ones are forgotten first.
    /// </summary>
    public const int MaxEntries = 500;

    /// <summary>
    /// The name shown as the source of the values offered.
    /// </summary>
    public const string SourceName = "Historique";

    private const int MaxValueLength = 200;

    // Kinds recognized through lists or labels, which a value can get from the field it was written into
    private static readonly HashSet<CandidateKind> UnshapedKinds = new()
    {
        CandidateKind.FirstName, CandidateKind.LastName, CandidateKind.FullName, CandidateKind.City, CandidateKind.Country,
        CandidateKind.Company,
    };

    private readonly IHistoryStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<HistoryEntry>? _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="FillHistory"/> class.
    /// </summary>
    /// <param name="store">The component that keeps the history between two launches.</param>
    public FillHistory(IHistoryStore store)
        : this(store, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FillHistory"/> class with a given clock.
    /// </summary>
    /// <param name="store">The component that keeps the history between two launches.</param>
    /// <param name="timeProvider">The clock used to date the uses.</param>
    public FillHistory(IHistoryStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Occurs when values written by the user or by a fill are recorded: the values are already trimmed, and the
    /// sensitive ones are never among them.
    /// </summary>
    public event EventHandler<IReadOnlyList<WrittenField>>? Recorded;

    /// <summary>
    /// Gets or sets a value indicating whether the history learns and offers values. When off, nothing is recorded
    /// and nothing is offered; what was learned before is kept.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Learns from the fills, the suggestions picked and the values undone.
    /// </summary>
    /// <param name="fillMemory">The memory of the fills.</param>
    public void Observe(FillMemory fillMemory)
    {
        ArgumentNullException.ThrowIfNull(fillMemory);

        fillMemory.Filled += async (_, written) => await RecordAsync(written, CancellationToken.None);
        fillMemory.Appended += async (_, written) => await RecordAsync(new[] { written }, CancellationToken.None);
        fillMemory.Undone += async (_, written) => await ForgetAsync(written, CancellationToken.None);
    }

    /// <summary>
    /// Records values written into fields.
    /// </summary>
    /// <param name="writtenFields">The values and the fields they were written into.</param>
    /// <param name="cancellationToken">A token to cancel the recording.</param>
    /// <returns>A task completed once the values are recorded and saved.</returns>
    public async Task RecordAsync(IEnumerable<WrittenField> writtenFields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writtenFields);

        var toRecord = writtenFields
            .Where(written => IsEnabled && written.Field.Kind == FieldKind.Text && !SensitiveFields.IsSensitive(written.Field, written.Value))
            .Select(written => written with { Value = written.Value.Trim() })
            .Where(written => written.Value.Length is > 0 and <= MaxValueLength)
            .ToList();
        if (toRecord.Count == 0)
        {
            return;
        }

        Recorded?.Invoke(this, toRecord);

        await ChangeAsync(
            entries =>
            {
                var now = _timeProvider.GetUtcNow();
                foreach (var written in toRecord)
                {
                    var index = entries.FindIndex(entry => IsSame(entry, written));
                    entries.Add(index < 0
                        ? NewEntry(written, now)
                        : entries[index] with { UseCount = entries[index].UseCount + 1, LastUsed = now });
                    if (index >= 0)
                    {
                        entries.RemoveAt(index);
                    }
                }

                // The least recently used values go first
                if (entries.Count > MaxEntries)
                {
                    entries.Sort((first, second) => second.LastUsed.CompareTo(first.LastUsed));
                    entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Takes back one use of values written into fields, because the user undid them: a value used often stays
    /// known, a value used once or twice is no longer offered.
    /// </summary>
    /// <param name="writtenFields">The values and the fields they were written into.</param>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>A task completed once the uses are taken back and the history saved.</returns>
    public async Task ForgetAsync(IEnumerable<WrittenField> writtenFields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writtenFields);

        var toForget = writtenFields.Select(written => written with { Value = written.Value.Trim() }).ToList();
        await ChangeAsync(
            entries =>
            {
                foreach (var written in toForget)
                {
                    var index = entries.FindIndex(entry => IsSame(entry, written));
                    if (index < 0)
                    {
                        continue;
                    }

                    if (entries[index].UseCount <= 1)
                    {
                        entries.RemoveAt(index);
                    }
                    else
                    {
                        entries[index] = entries[index] with { UseCount = entries[index].UseCount - 1 };
                    }
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Forgets everything learned.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the change.</param>
    /// <returns>A task completed once the history is empty and saved.</returns>
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return ChangeAsync(entries => entries.Clear(), cancellationToken);
    }

    /// <summary>
    /// Gets everything learned, the most recently used first.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The entries of the history.</returns>
    public async Task<IReadOnlyList<HistoryEntry>> GetEntriesAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadedEntriesAsync(cancellationToken);
            return entries.OrderByDescending(entry => entry.LastUsed).ToList();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Candidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return Array.Empty<Candidate>();
        }

        var entries = await GetEntriesAsync(cancellationToken);
        return entries
            .Where(entry => entry.UseCount >= MinUses)
            .Select(entry => new Candidate
            {
                Value = entry.Value,
                Kind = entry.Kind,
                Label = entry.FieldLabel.Length > 0 ? entry.FieldLabel : entry.TechnicalName,
                SourceName = SourceName,
                SourceRank = 0,
                IsFromHistory = true,
            })
            .ToList();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gate.Dispose();
    }

    /// <summary>
    /// Gets the kind of a value: the shape it has, such as an email, or else what the field expected, such as a
    /// first name.
    /// </summary>
    /// <param name="written">The value and its field.</param>
    /// <returns>The kind of the value.</returns>
    internal static CandidateKind KindOf(WrittenField written)
    {
        var shape = TextSegmenter.Segment(written.Value, ReferenceData.Empty)
            .FirstOrDefault(piece => string.Equals(piece.Value, written.Value, StringComparison.Ordinal))?.Kind ?? CandidateKind.Text;
        if (shape != CandidateKind.Text)
        {
            return shape;
        }

        // A value without shape never gets a kind that has one: "antoine51" typed into "Email ou identifiant" stays text
        var expected = FieldClues.Of(written.Field).ExpectedKinds.Where(UnshapedKinds.Contains).ToList();
        if (expected.Contains(CandidateKind.FullName) || expected.Contains(CandidateKind.LastName))
        {
            // A name field: the value itself tells a whole name from a last name, whatever the form was
            if (!written.Value.Contains(' ', StringComparison.Ordinal))
            {
                return CandidateKind.LastName;
            }

            var isWholeName = NameRecognizer.RecognizeSpan(written.Value, string.Empty, ReferenceData.Shared)
                .Any(piece => piece.Kind == CandidateKind.FullName);
            return isWholeName ? CandidateKind.FullName : CandidateKind.Text;
        }

        return expected.Count == 1 ? expected[0] : CandidateKind.Text;
    }

    private static bool IsSame(HistoryEntry entry, WrittenField written)
    {
        return string.Equals(entry.Value, written.Value, StringComparison.Ordinal)
            && string.Equals(ReferenceText.Normalize(entry.FieldLabel), ReferenceText.Normalize(written.Field.Label), StringComparison.Ordinal)
            && string.Equals(entry.TechnicalName, written.Field.TechnicalName, StringComparison.OrdinalIgnoreCase);
    }

    private static HistoryEntry NewEntry(WrittenField written, DateTimeOffset now)
    {
        return new HistoryEntry
        {
            FieldLabel = written.Field.Label.Trim(),
            TechnicalName = written.Field.TechnicalName,
            Value = written.Value,
            Kind = KindOf(written),
            UseCount = 1,
            LastUsed = now,
        };
    }

    private async Task ChangeAsync(Action<List<HistoryEntry>> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadedEntriesAsync(cancellationToken);
            change(entries);

            // Best effort: if saving fails, the history still works until the application closes
            _ = await _store.TrySaveAsync(entries.ToArray(), cancellationToken);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async Task<List<HistoryEntry>> LoadedEntriesAsync(CancellationToken cancellationToken)
    {
        _entries ??= (await _store.LoadAsync(cancellationToken)).ToList();
        return _entries;
    }
}
