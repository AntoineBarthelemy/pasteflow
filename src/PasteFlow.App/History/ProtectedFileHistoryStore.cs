using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    // Extra secret mixed into the encryption, so that another application of the same user cannot read the file by chance
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PasteFlow.History.v1");

    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedFileHistoryStore"/> class.
    /// </summary>
    /// <param name="path">The path of the encrypted file.</param>
    public ProtectedFileHistoryStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <summary>
    /// Gets the default path of the history: the local application data folder of the user.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "history.dat");

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<HistoryEntry>();
        }

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_path, cancellationToken);
            var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            SetAside();
            return Array.Empty<HistoryEntry>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TrySaveAsync(IReadOnlyList<HistoryEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.SerializeToUtf8Bytes(entries);
            var encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);

            // Written aside then swapped, so that a crash never leaves half a file
            var temporaryPath = _path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken);
            File.Move(temporaryPath, _path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void SetAside()
    {
        try
        {
            File.Move(_path, _path + ".unreadable", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing more can be done: the next save will replace it
        }
    }
}
