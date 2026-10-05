using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Field writer that records the written values instead of writing them.
/// </summary>
internal sealed class FakeFieldWriter : IFieldWriter
{
    private readonly Dictionary<string, string> _writtenValues = new();

    public ISet<string> FailingFieldIds { get; } = new HashSet<string>();

    public IReadOnlyDictionary<string, string> WrittenValues => _writtenValues;

    public Task<bool> TryWriteAsync(FormField field, string value, CancellationToken cancellationToken)
    {
        if (FailingFieldIds.Contains(field.Id))
        {
            return Task.FromResult(false);
        }

        _writtenValues[field.Id] = value;
        return Task.FromResult(true);
    }
}
