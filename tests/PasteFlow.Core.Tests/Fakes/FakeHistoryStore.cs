using PasteFlow.Core.History;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// History store kept in memory.
/// </summary>
internal sealed class FakeHistoryStore : IHistoryStore
{
    public IReadOnlyList<HistoryEntry> Saved { get; private set; } = Array.Empty<HistoryEntry>();

    public int LoadCount { get; private set; }

    public bool FailsToSave { get; set; }

    public Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        LoadCount++;
        return Task.FromResult(Saved);
    }

    public Task<bool> TrySaveAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken cancellationToken)
    {
        if (FailsToSave)
        {
            return Task.FromResult(false);
        }

        Saved = entries.ToArray();
        return Task.FromResult(true);
    }

    public void Preload(params HistoryEntry[] entries)
    {
        Saved = entries;
    }
}
