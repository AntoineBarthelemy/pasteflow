using PasteFlow.Core.History;

namespace PasteFlow.App.Tests.Fakes;

/// <summary>
/// History store kept in memory.
/// </summary>
internal sealed class InMemoryHistoryStore : IHistoryStore
{
    private IReadOnlyList<HistoryEntry> _entries = Array.Empty<HistoryEntry>();

    public Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_entries);
    }

    public Task<bool> TrySaveAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken cancellationToken)
    {
        _entries = entries.ToArray();
        return Task.FromResult(true);
    }
}
