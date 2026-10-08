namespace PasteFlow.Core.History;

/// <summary>
/// Keeps the history of fills between two launches of the application.
/// </summary>
public interface IHistoryStore
{
    /// <summary>
    /// Reads the history saved.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The entries saved; empty if there are none or they cannot be read. Never throws, except on cancellation.</returns>
    Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the history, replacing what was saved before.
    /// </summary>
    /// <param name="entries">The entries to save.</param>
    /// <param name="cancellationToken">A token to cancel the saving.</param>
    /// <returns><see langword="true"/> if the history was saved; otherwise <see langword="false"/>. Never throws, except on cancellation.</returns>
    Task<bool> TrySaveAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken cancellationToken);
}
