using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using PasteFlow.App.Interop;
using PasteFlow.App.Picking;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Matching;
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
    private const int MaxGroupDepth = 3;
    private const int DateBoxCount = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    // Words of the invite a list shows before a choice, accents and case aside
    private static readonly List<string> InviteWords = new() { "CHOISI", "SELECTION", "SELECT", "VEUILLEZ", "CHOOSE" };
    private static readonly TimeSpan WriteCheckTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan WriteCheckDelay = TimeSpan.FromMilliseconds(50);
    private static readonly string[] DayFirstDateFormats = { "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "dd-MM-yyyy" };

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
    /// Determines whether a text box only shows a value, such as the status bar of Notepad (« Ln 1, Col 1 », « UTF-8 »):
    /// it is no field of a form, nothing can be written there.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> for a text box that cannot be written; otherwise <see langword="false"/>.</returns>
    internal static bool IsReadOnlyBox(AutomationElement element)
    {
        return element.Current.ControlType == ControlType.Edit
            && element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            && pattern is ValuePattern { Current.IsReadOnly: true };
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
    /// Determines whether an element takes most of its window, as the page of a text editor does: a box of a form
    /// takes a small part of it.
    /// </summary>
    /// <param name="element">The element holding the cursor.</param>
    /// <returns><see langword="true"/> from 40 % of the window on; otherwise <see langword="false"/>.</returns>
    internal static bool FillsItsWindow(AutomationElement element)
    {
        const double MinShare = 0.4;
        try
        {
            var window = element;
            var walker = TreeWalker.ControlViewWalker;
            for (var parent = walker.GetParent(window); parent is not null && !Automation.Compare(parent, AutomationElement.RootElement); parent = walker.GetParent(window))
            {
                window = parent;
            }

            var bounds = element.Current.BoundingRectangle;
            var windowBounds = window.Current.BoundingRectangle;
            return !bounds.IsEmpty && !windowBounds.IsEmpty && windowBounds.Width * windowBounds.Height > 0
                && bounds.Width * bounds.Height / (windowBounds.Width * windowBounds.Height) >= MinShare;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // The element or its window vanished: it is not said to be a page
            return false;
        }
    }

    /// <summary>
    /// Determines whether the cursor is in the grid of a spreadsheet filling its window, as in Excel: a cell is no text
    /// box for Windows, and the boxes of the ribbon (name box, formula bar, font) are no form.
    /// </summary>
    /// <param name="element">The element holding the cursor: the grid or one of its cells.</param>
    /// <returns><see langword="true"/> in such a grid; otherwise <see langword="false"/>.</returns>
    internal static bool IsInSpreadsheetGrid(AutomationElement element)
    {
        try
        {
            var walker = TreeWalker.ControlViewWalker;
            var grid = element;
            for (var depth = 0; grid is not null && depth < 3; depth++, grid = walker.GetParent(grid))
            {
                if (grid.Current.ControlType == ControlType.DataGrid && grid.TryGetCurrentPattern(GridPattern.Pattern, out _))
                {
                    return FillsItsWindow(grid);
                }
            }

            return false;
        }
        catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // The element vanished: it is not said to be a grid
            return false;
        }
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
            IsDocument = (isEditable && FillsItsWindow(element)) || IsInSpreadsheetGrid(element),
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
            var options = kind == FieldKind.Choice ? OptionsOf(element, ControlType.ListItem) : new List<string>();

            return new FormField
            {
                Id = id,
                Label = ReadLabel(current),
                TechnicalName = current.AutomationId?.Trim() ?? string.Empty,
                Hint = current.HelpText?.Trim() ?? string.Empty,
                Kind = kind,
                HasValue = kind != FieldKind.Password && HasValue(element) && !(kind == FieldKind.Choice && ShowsInvite(element, options)),
                Options = options,
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

    // Tick boxes, and the groups holding radio buttons (a fieldset in a web page): one field per group
    private static List<AutomationElement> FindChoiceControls(AutomationElement window)
    {
        var scopes = window.FindAll(TreeScope.Descendants, DocumentCondition).Cast<AutomationElement>().ToList();
        if (scopes.Count == 0)
        {
            scopes.Add(window);
        }

        var condition = new AndCondition(
            new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton)),
            new PropertyCondition(AutomationElement.IsEnabledProperty, true));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var controls = new List<AutomationElement>();
        foreach (var scope in scopes)
        {
            foreach (AutomationElement element in scope.FindAll(TreeScope.Descendants, condition))
            {
                var control = element.Current.ControlType == ControlType.RadioButton ? GroupOf(element) : element;
                if (control is not null && seen.Add(string.Join('.', control.GetRuntimeId())))
                {
                    controls.Add(control);
                }
            }
        }

        return controls;
    }

    private static AutomationElement? GroupOf(AutomationElement radio)
    {
        var parent = TreeWalker.ControlViewWalker.GetParent(radio);
        for (var depth = 0; parent is not null && depth < MaxGroupDepth; depth++)
        {
            if (parent.Current.ControlType == ControlType.Group)
            {
                return parent;
            }

            parent = TreeWalker.ControlViewWalker.GetParent(parent);
        }

        return null;
    }

    private static FormField? TryReadChoiceControl(string id, AutomationElement element)
    {
        try
        {
            var current = element.Current;
            var label = ReadLabel(current);
            if (label.Length == 0)
            {
                return null;
            }

            if (current.ControlType == ControlType.CheckBox)
            {
                return new FormField
                {
                    Id = id,
                    Label = label,
                    TechnicalName = current.AutomationId?.Trim() ?? string.Empty,
                    Kind = FieldKind.CheckBox,
                    HasValue = IsTicked(element),
                };
            }

            var radios = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton))
                .Cast<AutomationElement>()
                .ToList();
            return new FormField
            {
                Id = id,
                Label = label,
                TechnicalName = current.AutomationId?.Trim() ?? string.Empty,
                Kind = FieldKind.Choice,
                HasValue = radios.Exists(IsSelected),
                Options = radios.Select(radio => radio.Current.Name.Trim()).Where(name => name.Length > 0).ToList(),
            };
        }
        catch (ElementNotAvailableException)
        {
            // The control disappeared while being read
            return null;
        }
    }

    private static List<string> OptionsOf(AutomationElement element, ControlType optionType)
    {
        return element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, optionType))
            .Cast<AutomationElement>()
            .Select(option => option.Current.Name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // A list still showing its first option or an invite, such as « Choisir… » or « -- Sélectionnez -- », has not
    // been chosen yet; a closed list of a web page does not give its options, the invite tells it
    private static bool ShowsInvite(AutomationElement element, List<string> options)
    {
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) || pattern is not ValuePattern valuePattern)
        {
            return false;
        }

        var shown = valuePattern.Current.Value?.Trim() ?? string.Empty;
        if (options.Count > 0 && string.Equals(shown, options[0], StringComparison.Ordinal))
        {
            return true;
        }

        var words = Core.Reference.ReferenceText.Normalize(shown);
        return shown.StartsWith("--", StringComparison.Ordinal) || shown.EndsWith('…') || shown.EndsWith("...", StringComparison.Ordinal)
            || InviteWords.Exists(word => words.Contains(word, StringComparison.Ordinal));
    }

    // Each box gets the focus, and its digits are typed only when it has it: nothing is ever typed elsewhere
    private static bool TryTypeDate(AutomationElement element, DateTime date)
    {
        var boxes = element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Spinner))
            .Cast<AutomationElement>()
            .ToList();
        var parts = boxes.Select(box => DatePartOf(box.Current.Name, date)).ToList();
        if (boxes.Count != DateBoxCount || parts.Exists(part => part is null))
        {
            return false;
        }

        for (var index = 0; index < boxes.Count; index++)
        {
            boxes[index].SetFocus();
            if (!WaitFor(() => HasFocus(boxes[index])) || !KeyboardTextInserter.TypeNow(parts[index]!.Value.Text))
            {
                return false;
            }
        }

        return WaitFor(() => boxes.Select((box, index) => ShowsNumber(box, parts[index]!.Value.Number)).All(isShown => isShown));
    }

    // « Jour Date de prise d'effet », « Mois … », « Année … » (or Day, Month, Year): the part of the date each box takes
    private static (string Text, int Number)? DatePartOf(string name, DateTime date)
    {
        var word = Core.Reference.ReferenceText.Normalize(name).Split(' ').FirstOrDefault() ?? string.Empty;
        return word switch
        {
            "JOUR" or "DAY" => (date.Day.ToString("00", CultureInfo.InvariantCulture), date.Day),
            "MOIS" or "MONTH" => (date.Month.ToString("00", CultureInfo.InvariantCulture), date.Month),
            "ANNEE" or "YEAR" => (date.Year.ToString(CultureInfo.InvariantCulture), date.Year),
            _ => null,
        };
    }

    private static bool HasFocus(AutomationElement element)
    {
        return AutomationElement.FocusedElement is { } focused && Automation.Compare(focused, element);
    }

    private static bool ShowsNumber(AutomationElement box, int number)
    {
        return box.TryGetCurrentPattern(RangeValuePattern.Pattern, out var pattern)
            && pattern is RangeValuePattern range
            && Math.Abs(range.Current.Value - number) < 0.5;
    }

    private static bool ShowsValue(AutomationElement element, string value)
    {
        return element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            && pattern is ValuePattern valuePattern
            && string.Equals(valuePattern.Current.Value?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTicked(AutomationElement element)
    {
        return element.TryGetCurrentPattern(TogglePattern.Pattern, out var pattern)
            && pattern is TogglePattern toggle
            && toggle.Current.ToggleState == ToggleState.On;
    }

    private static bool IsSelected(AutomationElement element)
    {
        return element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern)
            && pattern is SelectionItemPattern selection
            && selection.Current.IsSelected;
    }

    private static bool TryTick(AutomationElement element, string value)
    {
        if (!element.TryGetCurrentPattern(TogglePattern.Pattern, out var pattern) || pattern is not TogglePattern toggle)
        {
            return false;
        }

        // « oui » ticks the box, an empty value (cancelling a fill) unticks it
        var isWanted = string.Equals(value, ChoiceMatcher.Ticked, StringComparison.Ordinal);
        if (IsTicked(element) != isWanted)
        {
            toggle.Toggle();
        }

        return WaitFor(() => IsTicked(element) == isWanted);
    }

    private static bool TrySelect(AutomationElement element, string value)
    {
        var optionType = element.Current.ControlType == ControlType.Group ? ControlType.RadioButton : ControlType.ListItem;
        AutomationElement? FindOption() => element.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, optionType))
            .Cast<AutomationElement>()
            .FirstOrDefault(item => string.Equals(item.Current.Name.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));

        // A closed list of a web page shows its options only once opened: it is opened, then closed again
        var option = FindOption();
        var expander = option is null && element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand) ? expand as ExpandCollapsePattern : null;
        try
        {
            if (expander is not null)
            {
                expander.Expand();
                _ = WaitFor(() => FindOption() is not null);
                option = FindOption();
            }

            if (option is null || !option.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern) || pattern is not SelectionItemPattern selection)
            {
                return false;
            }

            selection.Select();
            return WaitFor(() => IsSelected(option) || ShowsValue(element, value));
        }
        finally
        {
            if (expander is not null && expander.Current.ExpandCollapseState == ExpandCollapseState.Expanded)
            {
                expander.Collapse();
            }
        }
    }

    // Waits a moment for applications such as browsers that show a change a little later
    private static bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WriteCheckTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(WriteCheckDelay);
        }

        return true;
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
            // A tick box is ticked, a list or a group of radio buttons gets the option named by the value
            var controlType = element.Current.ControlType;
            if (controlType == ControlType.CheckBox)
            {
                return TryTick(element, value);
            }

            if (controlType == ControlType.Group || (controlType == ControlType.ComboBox && !IsEditableComboBox(element)))
            {
                return TrySelect(element, value);
            }

            if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
                || pattern is not ValuePattern valuePattern
                || valuePattern.Current.IsReadOnly)
            {
                return false;
            }

            valuePattern.SetValue(value);
            if (Keeps(valuePattern, value, value))
            {
                return true;
            }

            // A date picker of a web page only takes the date as year-month-day, and shows it as it likes
            if (YearFirstDate(value) is { } yearFirst)
            {
                valuePattern.SetValue(yearFirst);
                if (Keeps(valuePattern, yearFirst, value))
                {
                    return true;
                }

                // A date picker of a web page takes a date only typed in its day, month and year boxes
                return DateTime.TryParseExact(yearFirst, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    && TryTypeDate(element, date);
            }

            // The field refused the value: it is not said written, so that the user is told
            return false;
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

    /// <summary>
    /// Determines whether a field kept a value just written, waiting a moment for applications such as browsers that
    /// show it a little later. Spaces, punctuation and case are not compared, since many fields reformat what they get.
    /// </summary>
    /// <param name="valuePattern">The field.</param>
    /// <param name="written">The value written.</param>
    /// <param name="shown">Another way the field may show the same value.</param>
    /// <returns><see langword="true"/> if the field shows the value, or cannot tell; otherwise <see langword="false"/>.</returns>
    private static bool Keeps(ValuePattern valuePattern, string written, string shown)
    {
        var deadline = DateTime.UtcNow + WriteCheckTimeout;
        while (true)
        {
            string current;
            try
            {
                current = valuePattern.Current.Value ?? string.Empty;
            }
            catch (InvalidOperationException)
            {
                // The field does not tell its value: what was written is trusted, as before
                return true;
            }

            if (SameText(current, written) || SameText(current, shown))
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(WriteCheckDelay);
        }
    }

    private static bool SameText(string current, string expected)
    {
        static string Core(string text) => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        var expectedCore = Core(expected);
        return expectedCore.Length == 0
            ? string.Equals(current.Trim(), expected.Trim(), StringComparison.Ordinal)
            : string.Equals(Core(current), expectedCore, StringComparison.Ordinal);
    }

    private static string? YearFirstDate(string value)
    {
        return DateTime.TryParseExact(value.Trim(), DayFirstDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
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
                    if (IsInsideComboBox(element) || !IsInPage(element, windowBounds) || IsReadOnlyBox(element))
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

                // Tick boxes and groups of radio buttons, after the text fields
                foreach (var element in FindChoiceControls(window))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsInPage(element, windowBounds))
                    {
                        continue;
                    }

                    var id = string.Create(CultureInfo.InvariantCulture, $"{reading}-{elements.Count}");
                    if (TryReadChoiceControl(id, element) is { } field)
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
