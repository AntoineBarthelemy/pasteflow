using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PasteFlow.App.Logins;

/// <summary>
/// Keeps the session key of the unlocked vault between two launches of PasteFlow, encrypted with the Windows account
/// of the user, so that the master password is not asked again.
/// </summary>
/// <remarks>
/// <para>Chosen by Antoine on 26 October 2026, for his own computer. The trade-off: the vault stays unlocked until it is
/// locked from the menu; Windows Hello is still asked before each password.</para>
/// <para>Only the same Windows account, on the same computer, can read the file. Locking from the menu deletes it.</para>
/// </remarks>
internal sealed class BitwardenSessionStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PasteFlow.Bitwarden.Session");

    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="BitwardenSessionStore"/> class.
    /// </summary>
    /// <param name="path">The encrypted file.</param>
    public BitwardenSessionStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
    }

    /// <summary>
    /// Gets the file of the session: %LOCALAPPDATA%\PasteFlow\bitwarden-session.bin.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "bitwarden-session.bin");

    /// <summary>
    /// Reads the session kept.
    /// </summary>
    /// <returns>The session key, or <see langword="null"/> if none is kept or the file cannot be read.</returns>
    public string? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var session = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser));
            return session.Length > 0 ? session : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Keeps a session for the next launches.
    /// </summary>
    /// <param name="session">The session key of the unlocked vault.</param>
    public void Save(string session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(session);

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllBytes(_path, ProtectedData.Protect(Encoding.UTF8.GetBytes(session), Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // Not kept: the master password is simply asked at the next launch
        }
    }

    /// <summary>
    /// Forgets the session kept.
    /// </summary>
    public void Delete()
    {
        try
        {
            File.Delete(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Already gone, or in use: an invalid session is refused at the next launch anyway
        }
    }
}
