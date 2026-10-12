using System.IO;
using PasteFlow.Core.History;

namespace PasteFlow.App.History;

/// <summary>
/// Keeps the history of fills in a file encrypted with the Windows account of the user (DPAPI).
/// </summary>
/// <remarks>
/// Only the same Windows account, on the same computer, can read the file: copied elsewhere, it is unreadable.
/// A file that cannot be read is set aside, never deleted, and the history starts again empty.
/// </remarks>
internal sealed class ProtectedFileHistoryStore : IHistoryStore
{
    private readonly ProtectedJsonFile<HistoryEntry> _file;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedFileHistoryStore"/> class.
    /// </summary>
    /// <param name="path">The path of the encrypted file.</param>
    public ProtectedFileHistoryStore(string path)
    {
        _file = new ProtectedJsonFile<HistoryEntry>(path, "PasteFlow.History.v1");
    }

    /// <summary>
    /// Gets the default path of the history: the local application data folder of the user.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "history.dat");

    /// <inheritdoc/>
    public Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        return _file.LoadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken cancellationToken)
    {
        return _file.TrySaveAsync(entries, cancellationToken);
    }
}
