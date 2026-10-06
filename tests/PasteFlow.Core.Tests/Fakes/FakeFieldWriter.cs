using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Fields kept in memory: written values are recorded and can be read back, as in a real form.
/// </summary>
internal sealed class FakeFieldWriter : IFieldWriter, IFieldValueReader
{
    private readonly Dictionary<string, string> _writtenValues = new();
    private readonly Dictionary<string, string> _currentValues = new();

    public ISet<string> FailingFieldIds { get; } = new HashSet<string>();

    public ISet<string> UnreadableFieldIds { get; } = new HashSet<string>();

    /// <summary>
    /// Gets the values written by the application, the last one for each field.
    /// </summary>
    public IReadOnlyDictionary<string, string> WrittenValues => _writtenValues;

    /// <summary>
    /// Gets the values the fields hold now, after the application and the user changed them.
    /// </summary>
    public IReadOnlyDictionary<string, string> CurrentValues => _currentValues;

    /// <summary>
    /// Simulates the user typing into a field.
    /// </summary>
    public void SimulateUserTyping(string fieldId, string value)
    {
        _currentValues[fieldId] = value;
    }

    public Task<bool> TryWriteAsync(FormField field, string value, CancellationToken cancellationToken)
    {
        if (FailingFieldIds.Contains(field.Id))
        {
            return Task.FromResult(false);
        }

        _writtenValues[field.Id] = value;
        _currentValues[field.Id] = value;
        return Task.FromResult(true);
    }

    public Task<string?> TryReadValueAsync(FormField field, CancellationToken cancellationToken)
    {
        return Task.FromResult(UnreadableFieldIds.Contains(field.Id) ? null : _currentValues.GetValueOrDefault(field.Id, string.Empty));
    }
}
