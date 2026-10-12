using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PasteFlow.App.History;

/// <summary>
/// A JSON file encrypted with the Windows account of the user (DPAPI).
/// </summary>
/// <typeparam name="T">The type of the items kept in the file.</typeparam>
/// <remarks>
/// Only the same Windows account, on the same computer, can read the file: copied elsewhere, it is unreadable.
/// A file that cannot be read is set aside, never deleted, and reading gives an empty list.
/// </remarks>
internal sealed class ProtectedJsonFile<T>
{
    private readonly string _path;
    private readonly byte[] _entropy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedJsonFile{T}"/> class.
    /// </summary>
    /// <param name="path">The path of the encrypted file.</param>
    /// <param name="purpose">
    /// An extra secret mixed into the encryption, different for each file, so that another application of the same
    /// user, or another file of PasteFlow, cannot read the file by chance.
    /// </param>
    public ProtectedJsonFile(string path, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        _path = path;
        _entropy = Encoding.UTF8.GetBytes(purpose);
    }

    /// <summary>
    /// Reads the items saved.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The items saved; empty if there are none or they cannot be read.</returns>
    public async Task<IReadOnlyList<T>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<T>();
        }

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_path, cancellationToken);
            var json = ProtectedData.Unprotect(encrypted, _entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            SetAside();
            return Array.Empty<T>();
        }
    }

    /// <summary>
    /// Saves the items, replacing what was saved before.
    /// </summary>
    /// <param name="items">The items to save.</param>
    /// <param name="cancellationToken">A token to cancel the saving.</param>
    /// <returns><see langword="true"/> if the items were saved; otherwise <see langword="false"/>.</returns>
    public async Task<bool> TrySaveAsync(IReadOnlyList<T> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.SerializeToUtf8Bytes(items);
            var encrypted = ProtectedData.Protect(json, _entropy, DataProtectionScope.CurrentUser);
            CryptographicOperations.ZeroMemory(json);

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
