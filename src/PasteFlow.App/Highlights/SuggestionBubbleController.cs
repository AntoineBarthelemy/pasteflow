using System.Globalization;
using System.Windows;
using PasteFlow.Core.Forms;
using PasteFlow.Core.Hotkeys;

namespace PasteFlow.App.Highlights;

/// <summary>
/// Shows a <see cref="SuggestionBubble"/> and writes the value the user picks with Ctrl+Shift+1, 2 or 3, or a click.
/// </summary>
/// <remarks>
/// <para>The shortcuts are registered only while the bubble is shown, so they never get in the way elsewhere.
/// Ctrl+Alt+digit is not used: on French keyboards it is AltGr+digit, which types "~" or "#".</para>
/// <para>Must be used from the user interface thread.</para>
/// </remarks>
internal sealed class SuggestionBubbleController : ISuggestionDisplay, IDisposable
{
    private readonly SuggestionPicker _picker;
    private readonly IReadOnlyList<IHotkeyListener> _choiceListeners;
    private readonly List<EventHandler> _choiceHandlers = new();
    private SuggestionBubble? _bubble;
    private FieldToCheck? _current;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuggestionBubbleController"/> class.
    /// </summary>
    /// <param name="picker">The component that writes the value picked.</param>
    /// <param name="choiceListeners">One shortcut listener per value that can be picked, owned by the caller.</param>
    public SuggestionBubbleController(SuggestionPicker picker, IReadOnlyList<IHotkeyListener> choiceListeners)
    {
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(choiceListeners);

        _picker = picker;
        _choiceListeners = choiceListeners;
        for (var index = 0; index < choiceListeners.Count; index++)
        {
            var position = index;
            EventHandler handler = async (_, _) => await PickAsync(position);
            _choiceHandlers.Add(handler);
            choiceListeners[index].HotkeyPressed += handler;
        }
    }

    /// <inheritdoc/>
    public event EventHandler? ValuePicked;

    /// <summary>
    /// Gets the identifier of the field whose suggestions are shown, or <see langword="null"/> when hidden.
    /// </summary>
    internal string? ShownFieldId => _current?.Field.Id;

    /// <summary>
    /// Gets the bubble, once it was shown at least once.
    /// </summary>
    internal SuggestionBubble? Bubble => _bubble;

    /// <summary>
    /// Gets the shortcut that picks a value.
    /// </summary>
    /// <param name="index">The position of the value, starting at 0.</param>
    /// <returns>Ctrl+Shift+1 for the first value, Ctrl+Shift+2 for the second...</returns>
    public static Hotkey ChoiceHotkey(int index)
    {
        return new Hotkey(KeyModifiers.Control | KeyModifiers.Shift, (char)('1' + index));
    }

    /// <inheritdoc/>
    public void Show(FieldToCheck fieldToCheck, Rect fieldBounds)
    {
        ArgumentNullException.ThrowIfNull(fieldToCheck);

        if (_isDisposed || fieldToCheck.Suggestions.Count == 0 || fieldBounds.IsEmpty)
        {
            Hide();
            return;
        }

        var bubble = _bubble ??= CreateBubble();
        if (_current?.Field.Id != fieldToCheck.Field.Id)
        {
            Hide();
            bubble.SetSuggestions(fieldToCheck.Suggestions, RegisterShortcuts(fieldToCheck.Suggestions.Count));
            _current = fieldToCheck;
        }

        bubble.ShowBelow(fieldBounds);
    }

    /// <inheritdoc/>
    public void Hide()
    {
        foreach (var listener in _choiceListeners)
        {
            listener.Unregister();
        }

        _bubble?.Hide();
        _current = null;
    }

    /// <summary>
    /// Writes a value of the suggestions shown into their field.
    /// </summary>
    /// <param name="index">The position of the value, starting at 0.</param>
    /// <returns><see langword="true"/> if the value was written; otherwise <see langword="false"/>.</returns>
    public async Task<bool> PickAsync(int index)
    {
        var current = _current;
        if (current is null)
        {
            return false;
        }

        Hide();
        var isWritten = await _picker.TryPickAsync(current, index, CancellationToken.None);
        if (isWritten)
        {
            ValuePicked?.Invoke(this, EventArgs.Empty);
        }

        return isWritten;
    }

    /// <summary>
    /// Hides the bubble and stops reacting to the shortcuts. The listeners themselves are left to their owner.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Hide();
        for (var index = 0; index < _choiceListeners.Count; index++)
        {
            _choiceListeners[index].HotkeyPressed -= _choiceHandlers[index];
        }

        _bubble?.Close();
        _isDisposed = true;
    }

    private static string ShortcutName(int index)
    {
        // "Maj" is the name of the Shift key on French keyboards
        return string.Create(CultureInfo.InvariantCulture, $"Ctrl+Maj+{index + 1}");
    }

    private SuggestionBubble CreateBubble()
    {
        var bubble = new SuggestionBubble();
        bubble.ValueClicked += async (_, index) => await PickAsync(index);
        return bubble;
    }

    private List<string> RegisterShortcuts(int suggestionCount)
    {
        var names = new List<string>(suggestionCount);
        for (var index = 0; index < suggestionCount; index++)
        {
            // A shortcut already used by another application is not shown: the value can still be clicked
            var isRegistered = index < _choiceListeners.Count && _choiceListeners[index].TryRegister(ChoiceHotkey(index));
            names.Add(isRegistered ? ShortcutName(index) : string.Empty);
        }

        return names;
    }
}
