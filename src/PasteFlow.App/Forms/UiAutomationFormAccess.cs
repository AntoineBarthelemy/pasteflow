using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using PasteFlow.App.Interop;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Picking;

namespace PasteFlow.App.Forms;

/// <summary>
/// Reads and writes the fields of the foreground window through Windows UI Automation, the accessibility API.
/// </summary>
/// <remarks>
/// Calls run on background threads, as recommended for UI Automation clients.
/// Values are never read from password fields.
/// Each reading gives its fields new identifiers, and the fields of the last few readings stay reachable,
/// so that a fill can be undone even after the shortcut was pressed in another window.
/// </remarks>
internal sealed class UiAutomationFormAccess : IFormReader, IFieldWriter, IFieldValueReader, ICaretReader
{
    /// <summary>
    /// The number of readings whose fields stay reachable.
    /// </summary>
    internal const int RememberedReadingCount = 3;

    private const int MaxReadAttempts = 2;
    private const int MaxTextBeforeCaret = 200;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly object _gate = new();
    private readonly Dictionary<string, RememberedField> _rememberedFields = new(StringComparer.Ordinal);
    private readonly Queue<string[]> _readingIds = new();
    private int _readingCount;

    /// <summary>
    /// Gets the condition that finds the pages of a window, such as the page shown by a browser.
    /// </summary>
    internal static Condition DocumentCondition { get; } = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);

    /// <summary>
    /// Gets the condition that finds the fields of a window: text boxes and combo boxes, enabled. Fields out of view
    /// further down a long form are kept; see <see cref="IsInPage"/> for those hidden on purpose.
    /// </summary>
    internal static Condition FieldCondition { get; } = new AndCondition(
        new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox)),
        new PropertyCondition(AutomationElement.IsEnabledProperty, true));

    /// <inheritdoc/>
    public Task<IReadOnlyList<FormField>> ReadActiveFormAsync(CancellationToken cancellationToken)
    {
        // The foreground window is captured right away, before anything else can take the focus
        return ReadFormAsync(NativeMethods.GetForegroundWindow(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> TryWriteAsync(FormField field, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(value);

        var element = FindElement(field);
        if (element is null)
        {
            // The field does not belong to a recent reading of the form
            return Task.FromResult(false);
        }

        AutomationElement target = element;
        return Task.Run(() => TryWrite(target, value), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string?> TryReadValueAsync(FormField field, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(field);

        var element = FindElement(field);
        if (element is null || field.Kind == FieldKind.Password)
        {
            return Task.FromResult<string?>(null);
        }

        AutomationElement target = element;
        return Task.Run(() => TryReadValue(target), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CaretContext?> ReadCaretAsync(CancellationToken cancellationToken)
    {
        return Task.Run(ReadCaret, cancellationToken);
    }

    /// <summary>
    /// Determines whether an element is a place where the user can type: a text box, an editable combo box, or a
    /// document showing a text cursor (a page being written, not a page being read).
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> if text can be typed there; otherwise <see langword="false"/>.</returns>
    internal static bool IsEditable(AutomationElement element)
    {
        var current = element.Current;
        if (current.IsPassword || !current.IsEnabled)
        {
            return false;
        }

        var valuePattern = element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ? value as ValuePattern : null;
        if (current.ControlType == ControlType.Edit)
        {
            return valuePattern is null || !valuePattern.Current.IsReadOnly;
        }

        if (current.ControlType == ControlType.ComboBox)
        {
            return IsEditableComboBox(element);
        }

        if (current.ControlType != ControlType.Document)
        {
            return false;
        }

        if (valuePattern is { Current.IsReadOnly: false })
        {
            return true;
        }

        // A document being written shows a blinking cursor: an empty selection
        return element.TryGetCurrentPattern(TextPattern.Pattern, out var text)
            && text is TextPattern textPattern
            && textPattern.GetSelection() is [var caret]
            && caret.CompareEndpoints(TextPatternRangeEndpoint.Start, caret, TextPatternRangeEndpoint.End) == 0;
    }

    /// <summary>
    /// Reads the text written just before the cursor of an element, on the current line or the lines above.
    /// </summary>
    /// <param name="element">The element holding the cursor.</param>
    /// <returns>Up to 200 characters before the cursor, or the end of the value when the cursor cannot be found.</returns>
    internal static string ReadTextBeforeCaret(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(TextPattern.Pattern, out var text) && text is TextPattern textPattern
            && textPattern.GetSelection() is { Length: > 0 } selection)
        {
            var before = selection[0].Clone();
            before.MoveEndpointByRange(TextPatternRangeEndpoint.End, selection[0], TextPatternRangeEndpoint.Start);
            _ = before.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, -MaxTextBeforeCaret);
            return before.GetText(MaxTextBeforeCaret);
        }

        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) && value is ValuePattern valuePattern)
        {
            var whole = valuePattern.Current.Value ?? string.Empty;
            return whole.Length > MaxTextBeforeCaret ? whole[^MaxTextBeforeCaret..] : whole;
        }

        return string.Empty;
    }

    /// <summary>
    /// Determines whether a field is part of the page a user can see, if need be by scrolling: a field without size,
    /// or moved beside the window, is hidden on purpose, often a trap for robots that a person must never fill.
    /// </summary>
    /// <param name="element">The field.</param>
    /// <param name="window">The position of its window, in screen pixels.</param>
    /// <returns><see langword="true"/> if the user can reach the field; otherwise <see langword="false"/>.</returns>
    internal static bool IsInPage(AutomationElement element, System.Windows.Rect window)
    {
        var bounds = element.Current.BoundingRectangle;
        const double MinSize = 2;
        return !bounds.IsEmpty
            && bounds.Width >= MinSize
            && bounds.Height >= MinSize
            && (window.IsEmpty || (bounds.Right > window.Left && bounds.Left < window.Right && bounds.Bottom > window.Top));
    }

    /// <summary>
    /// Tells a combo box where the user can type from a closed list. A combo box with its own text box can be typed
    /// in; one without, whose value can be set but which offers a selection, is a list of choices, such as a
    /// &lt;select&gt; of a web page.
    /// </summary>
    /// <param name="element">The combo box.</param>
    /// <returns><see langword="true"/> if the user can type in it; otherwise <see langword="false"/>.</returns>
    internal static bool IsEditableComboBox(AutomationElement element)
    {
        if (element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)) is not null)
        {
            return true;
        }

        // A box where the user types gives its text; a list of choices of a web page does not, and it opens a list
        if (element.TryGetCurrentPattern(TextPattern.Pattern, out _))
        {
            return true;
        }

        var isWritable = element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            && pattern is ValuePattern valuePattern
            && !valuePattern.Current.IsReadOnly;
        return isWritable
            && !element.TryGetCurrentPattern(SelectionPattern.Pattern, out _)
            && !element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out _);
    }

    /// <summary>
    /// Finds the fields of a window, in reading order. In a browser, only the fields of the page count: the address
    /// bar and the panels of the browser are not part of the form. A window whose pages hold no field, such as a mail
    /// application with a rich text body, keeps all its fields.
    /// </summary>
    /// <param name="window">The window.</param>
    /// <returns>The fields, each once.</returns>
    internal static List<AutomationElement> FindFields(AutomationElement window)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var fields = new List<AutomationElement>();
        foreach (AutomationElement document in window.FindAll(TreeScope.Descendants, DocumentCondition))
        {
            foreach (AutomationElement element in document.FindAll(TreeScope.Descendants, FieldCondition))
            {
                // A page inside another one (a frame) is found twice: each field is kept once
                if (seen.Add(string.Join('.', element.GetRuntimeId())))
                {
                    fields.Add(element);
                }
            }
        }

        return fields.Count > 0
            ? fields
            : window.FindAll(TreeScope.Descendants, FieldCondition).Cast<AutomationElement>().ToList();
    }

    /// <summary>
    /// Determines whether a text box belongs to a combo box, which stands for it.
    /// </summary>
    /// <param name="element">The text box.</param>
    /// <returns><see langword="true"/> if it is the text box of a combo box; otherwise <see langword="false"/>.</returns>
    internal static bool IsInsideComboBox(AutomationElement element)
    {
        try
        {
            return element.Current.ControlType == ControlType.Edit
                && TreeWalker.ControlViewWalker.GetParent(element) is { } parent
                && parent.Current.ControlType == ControlType.ComboBox;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the label of a field: its name, else the element that labels it, else its help text.
    /// </summary>
    /// <param name="current">The properties of the field.</param>
    /// <returns>The label, or an empty string.</returns>
    internal static string ReadLabel(AutomationElement.AutomationElementInformation current)
    {
        if (!string.IsNullOrWhiteSpace(current.Name))
        {
            return current.Name.Trim();
        }

        var labelElement = current.LabeledBy;
        if (labelElement is not null && !string.IsNullOrWhiteSpace(labelElement.Current.Name))
        {
            return labelElement.Current.Name.Trim();
        }

        // Web pages often expose the placeholder as help text
        return current.HelpText?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Describes the place of an element holding the cursor, tying it to a field of the last reading of the form.
    /// </summary>
    /// <param name="element">The element holding the cursor.</param>
    /// <returns>The place of the cursor.</returns>
    internal CaretContext DescribeCaret(AutomationElement element)
    {
        // The text box of a combo box is the combo box itself for the form
        var field = IsInsideComboBox(element) ? TreeWalker.ControlViewWalker.GetParent(element) ?? element : element;
        var isEditable = IsEditable(element);
        return new CaretContext
        {
            IsEditable = isEditable,
            FocusedFieldId = FindLastReadingId(field),
            FieldLabel = ReadLabel(field.Current),
            TextBefore = isEditable ? ReadTextBeforeCaret(element) : string.Empty,
        };
    }

    /// <summary>
    /// Gets the window a field was read in.
    /// </summary>
    /// <param name="field">The field, as returned by a recent reading of the form.</param>
    /// <returns>The window of the field, or <see cref="IntPtr.Zero"/> if the field is not from a recent reading.</returns>
    internal IntPtr GetWindow(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        lock (_gate)
        {
            return _rememberedFields.TryGetValue(field.Id, out var remembered) ? remembered.Window : IntPtr.Zero;
        }
    }

    /// <summary>
    /// Reads where fields are on the screen and whether they hold a value.
    /// </summary>
    /// <param name="fields">The fields, as returned by a recent reading of the form.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The state of each field that can still be read, by field identifier.</returns>
    internal Task<IReadOnlyDictionary<string, FieldState>> ReadStatesAsync(
        IReadOnlyList<FormField> fields,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var elements = new List<(string Id, AutomationElement Element)>(fields.Count);
        foreach (var field in fields)
        {
            var element = FindElement(field);
            if (element is not null)
            {
                elements.Add((field.Id, element));
            }
        }

        return Task.Run<IReadOnlyDictionary<string, FieldState>>(
            () =>
            {
                var states = new Dictionary<string, FieldState>(StringComparer.Ordinal);
                foreach (var (id, element) in elements)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (TryReadState(element) is { } state)
                    {
                        states[id] = state;
                    }
                }

                return states;
            },
            cancellationToken);
    }

    /// <summary>
    /// Reads the visible and enabled fields of a given window.
    /// </summary>
    /// <param name="windowHandle">The window to read.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The fields in reading order, or an empty list if the window cannot be read.</returns>
    internal Task<IReadOnlyList<FormField>> ReadFormAsync(IntPtr windowHandle, CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<FormField>>(() => ReadFields(windowHandle, cancellationToken), cancellationToken);
    }

    private static FormField? TryReadField(string id, AutomationElement element)
    {
        try
        {
            var current = element.Current;
            var kind = current.IsPassword ? FieldKind.Password : KindOf(element, current.ControlType);

            return new FormField
            {
                Id = id,
                Label = ReadLabel(current),
                TechnicalName = current.AutomationId?.Trim() ?? string.Empty,
                Hint = current.HelpText?.Trim() ?? string.Empty,
                Kind = kind,
                HasValue = kind != FieldKind.Password && HasValue(element),
            };
        }
        catch (ElementNotAvailableException)
        {
            // The field disappeared while being read
            return null;
        }
    }

    /// <summary>
    /// Tells a text field from a list of choices. A combo box where the user can type, such as a search box, an address
    /// bar or a field with suggestions, is a text field; a closed list such as a country picker is a list of choices.
    /// </summary>
    /// <param name="element">The field.</param>
    /// <param name="controlType">The type of control of the field.</param>
    /// <returns>The kind of the field.</returns>
    private static FieldKind KindOf(AutomationElement element, ControlType controlType)
    {
        if (controlType != ControlType.ComboBox)
        {
            return FieldKind.Text;
        }

        return IsEditableComboBox(element) ? FieldKind.Text : FieldKind.Choice;
    }

    private static bool HasValue(AutomationElement element)
    {
        return element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            && pattern is ValuePattern valuePattern
            && !string.IsNullOrWhiteSpace(valuePattern.Current.Value);
    }

    private static FieldState? TryReadState(AutomationElement element)
    {
        try
        {
            var current = element.Current;
            var bounds = current.IsOffscreen ? System.Windows.Rect.Empty : current.BoundingRectangle;
            return new FieldState(bounds, !current.IsPassword && HasValue(element), current.HasKeyboardFocus);
        }
        catch (ElementNotAvailableException)
        {
            // The field or its window disappeared
            return null;
        }
        catch (COMException)
        {
            // The application does not answer accessibility requests
            return null;
        }
    }

    private static string? TryReadValue(AutomationElement element)
    {
        try
        {
            // Checked again on the element itself: a password is never read
            if (element.Current.IsPassword
                || !element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
                || pattern is not ValuePattern valuePattern)
            {
                return null;
            }

            return valuePattern.Current.Value ?? string.Empty;
        }
        catch (ElementNotAvailableException)
        {
            // The field or its window disappeared
            return null;
        }
        catch (COMException)
        {
            // The application does not answer accessibility requests
            return null;
        }
    }

    private static bool TryWrite(AutomationElement element, string value)
    {
        try
        {
            if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
                || pattern is not ValuePattern valuePattern
                || valuePattern.Current.IsReadOnly)
            {
                return false;
            }

            valuePattern.SetValue(value);
            return true;
        }
        catch (ElementNotAvailableException)
        {
            // The field disappeared before being written
            return false;
        }
        catch (InvalidOperationException)
        {
            // The field refuses the value, for example because it is disabled
            return false;
        }
        catch (COMException)
        {
            // The application does not answer accessibility requests
            return false;
        }
    }

    private AutomationElement? FindElement(FormField field)
    {
        lock (_gate)
        {
            return _rememberedFields.TryGetValue(field.Id, out var remembered) ? remembered.Element : null;
        }
    }

    private List<FormField> ReadFields(IntPtr windowHandle, CancellationToken cancellationToken)
    {
        var elements = new Dictionary<string, AutomationElement>(StringComparer.Ordinal);
        var fields = new List<FormField>();
        var reading = Interlocked.Increment(ref _readingCount);

        for (var attempt = 1; attempt <= MaxReadAttempts && windowHandle != IntPtr.Zero; attempt++)
        {
            try
            {
                var window = AutomationElement.FromHandle(windowHandle);
                var windowBounds = window.Current.BoundingRectangle;
                foreach (var element in FindFields(window))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // The text box inside a combo box where the user can type is the combo box itself, read once;
                    // a field hidden on purpose is never filled
                    if (IsInsideComboBox(element) || !IsInPage(element, windowBounds))
                    {
                        continue;
                    }

                    // Unique across readings: a field of an old reading never targets an element of a new one
                    var id = string.Create(CultureInfo.InvariantCulture, $"{reading}-{elements.Count}");
                    var field = TryReadField(id, element);
                    if (field is not null)
                    {
                        elements[id] = element;
                        fields.Add(field);
                    }
                }

                break;
            }
            catch (ElementNotAvailableException)
            {
                // The window was closed while being read
                fields.Clear();
                elements.Clear();
                break;
            }
            catch (COMException)
            {
                // The application did not answer in time, often because it is busy: one more try
                fields.Clear();
                elements.Clear();
                if (attempt < MaxReadAttempts)
                {
                    Thread.Sleep(RetryDelay);
                }
            }
        }

        Remember(elements, windowHandle);
        return fields;
    }

    private CaretContext? ReadCaret()
    {
        try
        {
            return AutomationElement.FocusedElement is { } focused ? DescribeCaret(focused) : null;
        }
        catch (ElementNotAvailableException)
        {
            // The element lost the focus while being read
            return null;
        }
        catch (COMException)
        {
            // The application does not answer accessibility requests
            return null;
        }
        catch (InvalidOperationException)
        {
            // The text of the element cannot be read around the cursor
            return null;
        }
    }

    private string? FindLastReadingId(AutomationElement element)
    {
        lock (_gate)
        {
            var lastIds = _readingIds.LastOrDefault() ?? Array.Empty<string>();
            return lastIds.FirstOrDefault(id => _rememberedFields.TryGetValue(id, out var remembered) && Automation.Compare(remembered.Element, element));
        }
    }

    private void Remember(Dictionary<string, AutomationElement> elements, IntPtr windowHandle)
    {
        lock (_gate)
        {
            foreach (var (id, element) in elements)
            {
                _rememberedFields[id] = new RememberedField(element, windowHandle);
            }

            _readingIds.Enqueue(elements.Keys.ToArray());
            while (_readingIds.Count > RememberedReadingCount)
            {
                foreach (var id in _readingIds.Dequeue())
                {
                    _ = _rememberedFields.Remove(id);
                }
            }
        }
    }

    /// <summary>
    /// A field of a recent reading, with the window it was read in.
    /// </summary>
    /// <param name="Element">The accessibility element of the field.</param>
    /// <param name="Window">The window the field was read in.</param>
    private sealed record RememberedField(AutomationElement Element, IntPtr Window);
}
