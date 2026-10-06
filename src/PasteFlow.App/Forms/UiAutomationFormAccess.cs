using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using PasteFlow.App.Interop;
using PasteFlow.Core.Forms;

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
internal sealed class UiAutomationFormAccess : IFormReader, IFieldWriter, IFieldValueReader
{
    /// <summary>
    /// The number of readings whose fields stay reachable.
    /// </summary>
    internal const int RememberedReadingCount = 3;

    private static readonly Condition FieldCondition = new AndCondition(
        new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox)),
        new PropertyCondition(AutomationElement.IsEnabledProperty, true),
        new PropertyCondition(AutomationElement.IsOffscreenProperty, false));

    private readonly object _gate = new();
    private readonly Dictionary<string, RememberedField> _rememberedFields = new(StringComparer.Ordinal);
    private readonly Queue<string[]> _readingIds = new();
    private int _readingCount;

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
            var kind = current.IsPassword
                ? FieldKind.Password
                : current.ControlType == ControlType.ComboBox ? FieldKind.Choice : FieldKind.Text;

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

    private static string ReadLabel(AutomationElement.AutomationElementInformation current)
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

        if (windowHandle != IntPtr.Zero)
        {
            try
            {
                var window = AutomationElement.FromHandle(windowHandle);
                foreach (AutomationElement element in window.FindAll(TreeScope.Descendants, FieldCondition))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Unique across readings: a field of an old reading never targets an element of a new one
                    var id = string.Create(CultureInfo.InvariantCulture, $"{reading}-{elements.Count}");
                    var field = TryReadField(id, element);
                    if (field is not null)
                    {
                        elements[id] = element;
                        fields.Add(field);
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                // The window was closed while being read
                fields.Clear();
                elements.Clear();
            }
            catch (COMException)
            {
                // The application does not answer accessibility requests
                fields.Clear();
                elements.Clear();
            }
        }

        Remember(elements, windowHandle);
        return fields;
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
