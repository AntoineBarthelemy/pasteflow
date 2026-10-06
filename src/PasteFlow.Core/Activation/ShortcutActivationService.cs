using System.Diagnostics;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Notifications;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Activation;

/// <summary>
/// Connects the global shortcut to the actions of the application.
/// </summary>
/// <remarks>
/// Pressing the shortcut reads the form of the active window and, at the same time, the candidate sources.
/// Example values and placeholders are set aside (<see cref="SampleValues"/>), then each field allowed by
/// <see cref="FillRules"/> receives the value chosen by <see cref="FormMatcher"/>, only when the matcher is sure
/// enough and the value is not already given to another field; the other fields are left to the user and highlighted.
/// </remarks>
public sealed class ShortcutActivationService : IDisposable
{
    private readonly IHotkeyListener _listener;
    private readonly IFormReader _formReader;
    private readonly IFieldWriter _fieldWriter;
    private readonly ICandidateSource[] _candidateSources;
    private readonly IUserNotifier _notifier;
    private readonly FillMemory _fillMemory;
    private readonly IFieldHighlighter _highlighter;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortcutActivationService"/> class.
    /// </summary>
    /// <param name="listener">The listener of the global shortcut, owned by the caller.</param>
    /// <param name="formReader">The component that reads the form of the active window.</param>
    /// <param name="fieldWriter">The component that writes values into the fields read.</param>
    /// <param name="candidateSources">The places where values to paste are searched.</param>
    /// <param name="notifier">The component used to display messages to the user.</param>
    public ShortcutActivationService(
        IHotkeyListener listener,
        IFormReader formReader,
        IFieldWriter fieldWriter,
        IEnumerable<ICandidateSource> candidateSources,
        IUserNotifier notifier)
        : this(listener, formReader, fieldWriter, candidateSources, notifier, new FillMemory(), new NoHighlighter())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortcutActivationService"/> class that remembers its fills
    /// so that they can be undone, and highlights the fields left to the user.
    /// </summary>
    /// <param name="listener">The listener of the global shortcut, owned by the caller.</param>
    /// <param name="formReader">The component that reads the form of the active window.</param>
    /// <param name="fieldWriter">The component that writes values into the fields read.</param>
    /// <param name="candidateSources">The places where values to paste are searched.</param>
    /// <param name="notifier">The component used to display messages to the user.</param>
    /// <param name="fillMemory">The memory of the last fill, shared with the undo service.</param>
    /// <param name="highlighter">The component that shows the fields the user should check.</param>
    public ShortcutActivationService(
        IHotkeyListener listener,
        IFormReader formReader,
        IFieldWriter fieldWriter,
        IEnumerable<ICandidateSource> candidateSources,
        IUserNotifier notifier,
        FillMemory fillMemory,
        IFieldHighlighter highlighter)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(formReader);
        ArgumentNullException.ThrowIfNull(fieldWriter);
        ArgumentNullException.ThrowIfNull(candidateSources);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(fillMemory);
        ArgumentNullException.ThrowIfNull(highlighter);

        _listener = listener;
        _formReader = formReader;
        _fieldWriter = fieldWriter;
        _candidateSources = candidateSources.ToArray();
        _notifier = notifier;
        _fillMemory = fillMemory;
        _highlighter = highlighter;
    }

    /// <summary>
    /// Gets the shortcut currently listened to, or <see langword="null"/> if the service is not started.
    /// </summary>
    public Hotkey? ActiveHotkey { get; private set; }

    /// <summary>
    /// Gets or sets the shortcut that undoes a fill, announced to the user after each fill;
    /// <see langword="null"/> when undoing is not available.
    /// </summary>
    public Hotkey? UndoHotkey { get; set; }

    /// <summary>
    /// Starts listening to the given shortcut.
    /// </summary>
    /// <param name="hotkey">The shortcut to listen to.</param>
    /// <returns>
    /// <see langword="true"/> if the service is started; <see langword="false"/> if the shortcut is already used by another application.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is already started.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the service is disposed.</exception>
    public bool Start(Hotkey hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (ActiveHotkey is not null)
        {
            throw new InvalidOperationException("The service is already started.");
        }

        if (!_listener.TryRegister(hotkey))
        {
            return false;
        }

        _listener.HotkeyPressed += OnHotkeyPressed;
        ActiveHotkey = hotkey;
        return true;
    }

    /// <summary>
    /// Stops reacting to the shortcut. The listener itself is left to its owner.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _listener.HotkeyPressed -= OnHotkeyPressed;
        ActiveHotkey = null;
        _isDisposed = true;
    }

    private static List<SourceSummary> Summarize(IEnumerable<Candidate> candidates)
    {
        return candidates
            .GroupBy(candidate => (candidate.SourceRank, candidate.SourceName))
            .OrderBy(group => group.Key.SourceRank)
            .Select(group => new SourceSummary { Name = group.Key.SourceName, CandidateCount = group.Count() })
            .ToList();
    }

    private static Dictionary<CandidateKind, int> CountRecognized(IEnumerable<Candidate> candidates)
    {
        return candidates
            .Where(candidate => candidate.Kind != CandidateKind.Text)
            .DistinctBy(candidate => (candidate.Kind, candidate.Value))
            .GroupBy(candidate => candidate.Kind)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        var report = await FillActiveFormAsync(CancellationToken.None);
        if (_isDisposed)
        {
            return;
        }

        // An empty list also removes the frames of a previous fill
        _highlighter.Highlight(report.FieldsToCheck);
        _notifier.Notify(report.Describe());
    }

    private async Task<FillReport> FillActiveFormAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Sources are read while the form is being read
        var candidatesTask = GetAllCandidatesAsync(cancellationToken);
        var fields = await _formReader.ReadActiveFormAsync(cancellationToken);
        var allCandidates = await candidatesTask;

        // Safety net: example values and placeholders are never pasted
        var hints = fields.Select(field => field.Hint).Where(hint => hint.Length > 0).ToList();
        var candidates = allCandidates
            .Where(candidate => !SampleValues.IsSample(candidate) && !SampleValues.AppearsInHints(candidate.Value, hints))
            .ToList();
        var ignoredSampleCount = allCandidates
            .Except(candidates)
            .Select(candidate => candidate.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var filledValues = new List<FilledValue>();
        var writtenFields = new List<WrittenField>();
        var uncertainCount = 0;
        var failedCount = 0;
        var fieldsToCheck = new List<FormField>();

        // Safety net: the values are chosen for the whole form at once, so one value never fills two fields
        var fillableFields = fields.Where(FillRules.CanFill).ToList();
        foreach (var match in FormMatcher.MatchAll(fillableFields, candidates))
        {
            var field = match.Field;
            if (!match.IsConfident || match.Candidate is null)
            {
                uncertainCount++;
                fieldsToCheck.Add(field);
            }
            else if (await _fieldWriter.TryWriteAsync(field, match.Candidate.Value, cancellationToken))
            {
                filledValues.Add(new FilledValue
                {
                    FieldLabel = field.Label,
                    Value = match.Candidate.Value,
                    SourceName = match.Candidate.SourceName,
                });
                writtenFields.Add(new WrittenField { Field = field, Value = match.Candidate.Value });
            }
            else
            {
                failedCount++;
                fieldsToCheck.Add(field);
            }
        }

        stopwatch.Stop();
        _fillMemory.Remember(writtenFields);

        return new FillReport
        {
            FilledCount = filledValues.Count,
            UncertainCount = uncertainCount,
            AlreadyFilledCount = fields.Count(field => field.Kind == FieldKind.Text && field.HasValue),
            FailedCount = failedCount,
            FilledValues = filledValues,
            Sources = Summarize(allCandidates),
            RecognizedCounts = CountRecognized(candidates),
            IgnoredSampleCount = ignoredSampleCount,
            Elapsed = stopwatch.Elapsed,
            FieldsToCheck = fieldsToCheck,
            UndoHotkey = UndoHotkey,
        };
    }

    private async Task<List<Candidate>> GetAllCandidatesAsync(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(_candidateSources.Select(source => source.GetCandidatesAsync(cancellationToken)));
        return results.SelectMany(candidates => candidates).ToList();
    }

    /// <summary>
    /// Highlighter used when the caller does not show the fields to check.
    /// </summary>
    private sealed class NoHighlighter : IFieldHighlighter
    {
        public void Highlight(IReadOnlyList<FormField> fields)
        {
            // Nothing to show
        }
    }
}
