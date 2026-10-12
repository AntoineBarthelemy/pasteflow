using System.Diagnostics;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Notifications;
using PasteFlow.Core.Picking;
using PasteFlow.Core.Reference;
using PasteFlow.Core.Screen;
using PasteFlow.Core.Security;
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
/// <para>Fields already filled are left intact. Pressing the shortcut again on the same form within
/// <see cref="ReplaceDelay"/> also replaces their values, only with values sure enough, and only when their current
/// value can be read so that undoing puts it back.</para>
/// </remarks>
public sealed class ShortcutActivationService : IDisposable
{
    private const int MaxNotifiedLength = 60;
    private const string CannotPasteMessage = "Impossible d'écrire à cet endroit.";
    private const string SensitiveFillReason = "Remplir une info sensible de ta fiche PasteFlow (IBAN, n° de sécu…)";

    private readonly object _pressGate = new();
    private readonly IHotkeyListener _listener;
    private readonly IFormReader _formReader;
    private readonly IFieldWriter _fieldWriter;
    private readonly ICandidateSource[] _candidateSources;
    private readonly IUserNotifier _notifier;
    private readonly FillMemory _fillMemory;
    private readonly IFieldHighlighter _highlighter;
    private DateTimeOffset? _lastPressTime;
    private string? _lastFormSignature;
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
    /// Gets the longest time between two presses on the same form for the second one to replace the values already there.
    /// </summary>
    public static TimeSpan ReplaceDelay { get; } = TimeSpan.FromSeconds(8);

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
    /// Gets or sets how sure a value must be to fill a field without asking; balanced by default.
    /// </summary>
    public MatchingOptions Options { get; set; } = MatchingOptions.Balanced;

    /// <summary>
    /// Gets or sets the clock that tells whether two presses are close enough for the second one to replace values.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets or sets the gate that guards the sensitive values of the profile: they are written or proposed only once
    /// the user proved it is them. Without a gate, they are never used.
    /// </summary>
    public SensitiveGate? SensitiveGate { get; set; }

    /// <summary>
    /// Gets or sets the component that tells where the text cursor is; with <see cref="Picker"/> and
    /// <see cref="Inserter"/>, it lets the shortcut paste in a text written alone, such as a message or a document.
    /// </summary>
    public ICaretReader? CaretReader { get; set; }

    /// <summary>
    /// Gets or sets the list shown at the cursor when nothing is sure.
    /// </summary>
    public IPastePicker? Picker { get; set; }

    /// <summary>
    /// Gets or sets the component that writes at the cursor.
    /// </summary>
    public ITextInserter? Inserter { get; set; }

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

    /// <summary>
    /// Describes a form by its fields, whatever their values, to recognize it when the shortcut is pressed again.
    /// </summary>
    /// <param name="fields">The fields of the form.</param>
    /// <returns>The labels and technical names of the fields, in order.</returns>
    private static string SignatureOf(IReadOnlyList<FormField> fields)
    {
        return string.Join('\n', fields.Select(field => $"{field.Kind}|{field.Label}|{field.TechnicalName}"));
    }

    private static Dictionary<CandidateKind, int> CountRecognized(IEnumerable<Candidate> candidates)
    {
        return candidates
            .Where(candidate => candidate.Kind != CandidateKind.Text)
            .DistinctBy(candidate => (candidate.Kind, candidate.Value))
            .GroupBy(candidate => candidate.Kind)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static bool UsesSensitiveValue(FieldMatch match)
    {
        return match is { IsConfident: true, Candidate.IsSensitive: true } || match.Suggestions.Any(candidate => candidate.IsSensitive);
    }

    private static FieldMatch WithoutSensitiveValues(FieldMatch match)
    {
        var isSensitive = match.Candidate is { IsSensitive: true };
        return match with
        {
            Candidate = isSensitive ? null : match.Candidate,
            IsConfident = match.IsConfident && !isSensitive,
            Suggestions = match.Suggestions.Where(candidate => !candidate.IsSensitive).ToList(),
        };
    }

    /// <summary>
    /// Removes from the values read in the form's own window the values its filled fields hold.
    /// </summary>
    /// <param name="candidates">The values found.</param>
    /// <param name="fields">The fields of the form.</param>
    /// <param name="valueReader">The component that reads the value of a field.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The values left.</returns>
    private static async Task<List<Candidate>> WithoutCurrentValuesAsync(
        List<Candidate> candidates,
        IReadOnlyList<FormField> fields,
        IFieldValueReader valueReader,
        CancellationToken cancellationToken)
    {
        var currentValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields.Where(FillRules.CanReplace))
        {
            if (await valueReader.TryReadValueAsync(field, cancellationToken) is { } value && value.Trim().Length > 0)
            {
                _ = currentValues.Add(value.Trim());
            }
        }

        return candidates
            .Where(candidate => candidate.SourceRank != 0 || !currentValues.Contains(candidate.Value.Trim()))
            .ToList();
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        var report = await FillActiveFormAsync(CancellationToken.None);
        if (_isDisposed || report is null)
        {
            // Pasted at the cursor, or stopped: there is no form to report on
            return;
        }

        // An empty list also removes the frames of a previous fill
        _highlighter.Highlight(report.FieldsToCheck);
        _notifier.NotifyFill(report);
    }

    private async Task<FillReport?> FillActiveFormAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var pressTime = TimeProvider.GetUtcNow();

        // Sources are read while the form is being read
        var candidatesTask = GetAllCandidatesAsync(cancellationToken);
        var fields = await _formReader.ReadActiveFormAsync(cancellationToken);
        var allCandidates = await candidatesTask;

        // Safety net: values are replaced only when they can be put back by undoing
        var valueReader = IsSecondPress(fields, pressTime) ? _fieldWriter as IFieldValueReader : null;
        var isReplacing = valueReader is not null;

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

        // A text written alone, such as a message or a document, is filled at the cursor rather than as a form
        if (await TryPasteAtCaretAsync(fields, candidates, cancellationToken))
        {
            return null;
        }

        var filledValues = new List<FilledValue>();
        var writtenFields = new List<WrittenField>();
        var uncertainCount = 0;
        var failedCount = 0;
        var fieldsToCheck = new List<FieldToCheck>();
        var failedFields = new List<FormField>();
        var replacedCount = 0;

        // A browser shows the values of the fields in the text of the page: when replacing, the value already in a
        // field is the one being replaced, never a candidate competing with the new one
        if (isReplacing)
        {
            candidates = await WithoutCurrentValuesAsync(candidates, fields, valueReader!, cancellationToken);
        }

        // Safety net: the values are chosen for the whole form at once, so one value never fills two fields
        var fillableFields = fields
            .Where(field => FillRules.CanFill(field) || (isReplacing && FillRules.CanReplace(field)))
            .ToList();
        var textFields = fields.Where(field => field.Kind == FieldKind.Text).ToList();
        var page = PageContext.From(_candidateSources.OfType<IScreenTextSource>()
            .SelectMany(source => source.LastWindows)
            .FirstOrDefault(window => window.Rank == 0));
        var matches = FormMatcher.MatchAll(fillableFields, candidates, ReferenceData.Shared, Options, textFields, page);
        foreach (var match in await GuardSensitiveValuesAsync(matches, cancellationToken))
        {
            var field = match.Field;
            if (field.HasValue)
            {
                if (await TryReplaceAsync(match, valueReader!, cancellationToken) is { } replaced)
                {
                    replacedCount++;
                    filledValues.Add(new FilledValue
                    {
                        FieldLabel = field.Label,
                        Value = replaced.Value,
                        SourceName = match.Candidate!.SourceName,
                    });
                    writtenFields.Add(replaced);
                }

                continue;
            }

            if (!match.IsConfident || match.Candidate is null)
            {
                uncertainCount++;
                fieldsToCheck.Add(new FieldToCheck
                {
                    Field = field,
                    Suggestions = match.Suggestions
                        .Select(candidate => new SuggestedValue
                        {
                            Value = ValueFormatter.Adapt(candidate.Value, candidate.Kind, field),
                            SourceName = candidate.SourceName,
                        })
                        .ToList(),
                });
                continue;
            }

            // Written in the format the field shows, such as "JJ/MM/AAAA"
            var value = ValueFormatter.Adapt(match.Candidate.Value, match.Candidate.Kind, field);
            if (await _fieldWriter.TryWriteAsync(field, value, cancellationToken))
            {
                filledValues.Add(new FilledValue
                {
                    FieldLabel = field.Label,
                    Value = value,
                    SourceName = match.Candidate.SourceName,
                });
                writtenFields.Add(new WrittenField { Field = field, Value = value });
            }
            else
            {
                failedCount++;

                // The value was refused: proposing it again would not help
                fieldsToCheck.Add(new FieldToCheck { Field = field });
                failedFields.Add(field);
            }
        }

        stopwatch.Stop();

        // The page tells what the values were for: the profile learns only from the fields about the user
        _fillMemory.Remember(writtenFields.Select(written => written with { Page = page }).ToList());

        return new FillReport
        {
            FilledCount = filledValues.Count - replacedCount,
            UncertainCount = uncertainCount,
            AlreadyFilledCount = fields.Count(FillRules.CanReplace) - replacedCount,
            ReplacedCount = replacedCount,
            FailedCount = failedCount,
            FilledValues = filledValues,
            Sources = Summarize(allCandidates.Where(candidate => !candidate.IsFromHistory && !candidate.IsFromProfile)),
            RecognizedCounts = CountRecognized(candidates.Where(candidate => !candidate.IsFromHistory && !candidate.IsFromProfile)),
            IgnoredSampleCount = ignoredSampleCount,
            Elapsed = stopwatch.Elapsed,
            FieldsToCheck = fieldsToCheck,
            FailedFields = failedFields,
            UndoHotkey = UndoHotkey,
            ReplaceHotkey = isReplacing ? null : ActiveHotkey,
        };
    }

    /// <summary>
    /// Pastes at the cursor when it is in a text written alone: the value asked by the words before the cursor when
    /// it is sure, else the choice of the user among the likely values and the text of the windows.
    /// </summary>
    /// <param name="fields">The fields of the active window.</param>
    /// <param name="candidates">The values found, examples set aside.</param>
    /// <param name="cancellationToken">A token to cancel the paste.</param>
    /// <returns><see langword="true"/> if the cursor was in a text written alone; <see langword="false"/> to fill the form.</returns>
    private async Task<bool> TryPasteAtCaretAsync(IReadOnlyList<FormField> fields, List<Candidate> candidates, CancellationToken cancellationToken)
    {
        if (CaretReader is null || Picker is null || Inserter is null)
        {
            return false;
        }

        var caret = await CaretReader.ReadCaretAsync(cancellationToken);
        if (caret is null || !FreePaste.IsFreeText(fields, caret))
        {
            return false;
        }

        // The frames of a previous fill have nothing to do with this text
        _highlighter.Highlight(Array.Empty<FieldToCheck>());

        var field = FreePaste.TargetField(caret);
        candidates = candidates.Where(candidate => !FreePaste.IsTypedText(candidate, field) && !candidate.IsFromProfile).ToList();
        var match = FormMatcher.MatchAll(new[] { field }, candidates, ReferenceData.Shared, Options, new[] { field })[0];
        if (match is { IsConfident: true, Candidate: { } sure })
        {
            var value = ValueFormatter.Adapt(sure.Value, sure.Kind, field);
            var isPasted = await Inserter.InsertAsync(value, cancellationToken);
            _notifier.Notify(isPasted ? $"Collé : {FillReport.Shorten(value, MaxNotifiedLength)} ({sure.SourceName})" : CannotPasteMessage);
            return true;
        }

        var screenCandidates = candidates.Where(candidate => !candidate.IsFromHistory && !candidate.IsFromProfile).ToList();
        var windows = _candidateSources.OfType<IScreenTextSource>().SelectMany(source => source.LastWindows).ToList();
        var request = new PickRequest
        {
            Suggestions = FieldMatcher.Rank(
                FieldClues.Of(field),
                screenCandidates,
                ScreenContext.From(screenCandidates),
                PassageFinder.MaxResults),
            Finder = new PassageFinder(windows, screenCandidates),
        };
        var chosen = await Picker.PickAsync(request, cancellationToken);
        if (chosen is not null && !await Inserter.InsertAsync(chosen, cancellationToken))
        {
            _notifier.Notify(CannotPasteMessage);
        }

        return true;
    }

    /// <summary>
    /// Tells whether the shortcut was just pressed on the same form, and remembers this press.
    /// </summary>
    /// <param name="fields">The fields of the form.</param>
    /// <param name="pressTime">When the shortcut was pressed.</param>
    /// <returns><see langword="true"/> if the user asks to replace the values already there; otherwise <see langword="false"/>.</returns>
    private bool IsSecondPress(IReadOnlyList<FormField> fields, DateTimeOffset pressTime)
    {
        var signature = fields.Count > 0 ? SignatureOf(fields) : null;
        lock (_pressGate)
        {
            var isSecondPress = signature is not null
                && string.Equals(signature, _lastFormSignature, StringComparison.Ordinal)
                && _lastPressTime is { } lastPressTime
                && pressTime - lastPressTime <= ReplaceDelay;
            _lastPressTime = pressTime;
            _lastFormSignature = signature;
            return isSecondPress;
        }
    }

    /// <summary>
    /// Replaces the value of a field already filled with a value sure enough, keeping the previous one to undo.
    /// </summary>
    /// <param name="match">The value chosen for the field.</param>
    /// <param name="valueReader">The component that reads the value the field holds.</param>
    /// <param name="cancellationToken">A token to cancel the writing.</param>
    /// <returns>The value written, or <see langword="null"/> if the field is left as it is.</returns>
    private async Task<WrittenField?> TryReplaceAsync(FieldMatch match, IFieldValueReader valueReader, CancellationToken cancellationToken)
    {
        if (!match.IsConfident || match.Candidate is null)
        {
            // In doubt, the value of the user stays
            return null;
        }

        var field = match.Field;
        var value = ValueFormatter.Adapt(match.Candidate.Value, match.Candidate.Kind, field);
        var previousValue = await valueReader.TryReadValueAsync(field, cancellationToken);
        if (previousValue is null || UndoRules.IsStillWritten(value, previousValue))
        {
            // Unreadable, it could not be put back; already the same value, nothing to replace
            return null;
        }

        return await _fieldWriter.TryWriteAsync(field, value, cancellationToken)
            ? new WrittenField { Field = field, Value = value, PreviousValue = previousValue }
            : null;
    }

    /// <summary>
    /// Safety net: a sensitive value of the profile, such as an IBAN, is written or proposed only once the user proved
    /// it is them; otherwise it is left out, and the field is left to the user.
    /// </summary>
    /// <param name="matches">The matches of the form.</param>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>The matches, without sensitive values if the user did not prove it is them.</returns>
    private async Task<IReadOnlyList<FieldMatch>> GuardSensitiveValuesAsync(IReadOnlyList<FieldMatch> matches, CancellationToken cancellationToken)
    {
        if (!matches.Any(UsesSensitiveValue))
        {
            return matches;
        }

        if (SensitiveGate is { } gate && await gate.OpenAsync(SensitiveFillReason, cancellationToken) == PresenceResult.Verified)
        {
            return matches;
        }

        return matches.Select(WithoutSensitiveValues).ToList();
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
        public void Highlight(IReadOnlyList<FieldToCheck> fields)
        {
            // Nothing to show
        }
    }
}
