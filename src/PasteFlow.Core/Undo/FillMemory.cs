namespace PasteFlow.Core.Undo;

/// <summary>
/// Remembers the values written by the last fill that wrote something, so that it can be undone.
/// </summary>
/// <remarks>
/// Only the last fill is kept, in memory only: nothing is saved on disk. A fill that writes nothing does not
/// replace the previous one, so pressing the fill shortcut in the wrong window does not prevent undoing.
/// </remarks>
public sealed class FillMemory
{
    private readonly object _gate = new();
    private IReadOnlyList<WrittenField> _lastFill = Array.Empty<WrittenField>();

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

        lock (_gate)
        {
            _lastFill = writtenFields.ToArray();
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
