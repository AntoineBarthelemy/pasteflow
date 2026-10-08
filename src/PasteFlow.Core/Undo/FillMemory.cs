namespace PasteFlow.Core.Undo;

/// <summary>
/// Remembers the values written by the last fill that wrote something, so that it can be undone.
/// </summary>
/// <remarks>
/// Only the last fill is kept, in memory only: nothing is saved on disk. A fill that writes nothing does not
/// replace the previous one, so pressing the fill shortcut in the wrong window does not prevent undoing.
/// Its events let the history of fills learn from what is written and undone.
/// </remarks>
public sealed class FillMemory
{
    private readonly object _gate = new();
    private IReadOnlyList<WrittenField> _lastFill = Array.Empty<WrittenField>();

    /// <summary>
    /// Occurs when a fill wrote values.
    /// </summary>
    public event EventHandler<IReadOnlyList<WrittenField>>? Filled;

    /// <summary>
    /// Occurs when a value was written after the fill, such as a suggestion picked by the user.
    /// </summary>
    public event EventHandler<WrittenField>? Appended;

    /// <summary>
    /// Occurs when the user undid values of the last fill.
    /// </summary>
    public event EventHandler<IReadOnlyList<WrittenField>>? Undone;

    /// <summary>
    /// Remembers the values written by a fill.
    /// </summary>
    /// <param name="writtenFields">The values written, in the order of the form.</param>
    public void Remember(IReadOnlyList<WrittenField> writtenFields)
    {
        ArgumentNullException.ThrowIfNull(writtenFields);

        if (writtenFields.Count == 0)
        {
            return;
        }

        var copy = writtenFields.ToArray();
        lock (_gate)
        {
            _lastFill = copy;
        }

        Filled?.Invoke(this, copy);
    }

    /// <summary>
    /// Adds a value written after the fill, such as a suggestion picked by the user, to the last fill.
    /// </summary>
    /// <param name="writtenField">The value written.</param>
    public void Append(WrittenField writtenField)
    {
        ArgumentNullException.ThrowIfNull(writtenField);

        lock (_gate)
        {
            _lastFill = _lastFill.Append(writtenField).ToArray();
        }

        Appended?.Invoke(this, writtenField);
    }

    /// <summary>
    /// Tells that the user undid values: the fields were emptied again.
    /// </summary>
    /// <param name="emptiedFields">The values undone.</param>
    public void ReportUndone(IReadOnlyList<WrittenField> emptiedFields)
    {
        ArgumentNullException.ThrowIfNull(emptiedFields);

        if (emptiedFields.Count > 0)
        {
            Undone?.Invoke(this, emptiedFields);
        }
    }

    /// <summary>
    /// Gives the values of the last fill and forgets them, so that a fill is undone only once.
    /// </summary>
    /// <returns>The values written by the last fill, or an empty list if there is nothing to undo.</returns>
    public IReadOnlyList<WrittenField> TakeLastFill()
    {
        lock (_gate)
        {
            var lastFill = _lastFill;
            _lastFill = Array.Empty<WrittenField>();
            return lastFill;
        }
    }
}
