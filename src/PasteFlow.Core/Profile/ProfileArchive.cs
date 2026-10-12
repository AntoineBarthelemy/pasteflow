using System.Security.Cryptography;
using System.Text.Json;

namespace PasteFlow.Core.Profile;

/// <summary>
/// Exports the profile into a file protected by a password, to bring it to another computer, and reads it back.
/// </summary>
/// <remarks>
/// <para>The profile saved on the computer is encrypted with the Windows account, so it cannot be read anywhere else.
/// The export is encrypted with a key drawn from the password instead (PBKDF2, SHA-256), with AES-GCM, which also
/// detects any change made to the file.</para>
/// <para>Layout of the file: header, salt, nonce, tag, encrypted values.</para>
/// </remarks>
public static class ProfileArchive
{
    /// <summary>
    /// The minimum length of the password of an export.
    /// </summary>
    public const int MinPasswordLength = 8;

    /// <summary>
    /// The extension of the export files.
    /// </summary>
    public const string FileExtension = ".pasteflow";

    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32;

    // Slow on purpose, so that guessing the password of a stolen export takes very long
    private const int Iterations = 600_000;

    private static readonly byte[] Header = "PASTEFLOW-PROFILE-1"u8.ToArray();

    /// <summary>
    /// Exports values of the profile.
    /// </summary>
    /// <param name="values">The values to export.</param>
    /// <param name="password">The password that protects the export, at least <see cref="MinPasswordLength"/> characters.</param>
    /// <returns>The content of the export file.</returns>
    /// <exception cref="ArgumentException">The password is too short.</exception>
    public static byte[] Export(IReadOnlyList<ProfileValue> values, string password)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length < MinPasswordLength)
        {
            throw new ArgumentException($"Le mot de passe doit faire au moins {MinPasswordLength} caractères.", nameof(password));
        }

        var plain = JsonSerializer.SerializeToUtf8Bytes(values);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var tag = new byte[TagLength];
        var encrypted = new byte[plain.Length];
        var key = DeriveKey(password, salt);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Encrypt(nonce, plain, encrypted, tag, Header);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }

        return [.. Header, .. salt, .. nonce, .. tag, .. encrypted];
    }

    /// <summary>
    /// Reads an export of the profile.
    /// </summary>
    /// <param name="archive">The content of the export file.</param>
    /// <param name="password">The password given when the export was made.</param>
    /// <returns>The values of the export, or why they could not be read.</returns>
    public static ProfileArchiveReading Read(byte[] archive, string password)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(password);

        var dataStart = Header.Length + SaltLength + NonceLength + TagLength;
        if (archive.Length < dataStart || !archive.AsSpan(0, Header.Length).SequenceEqual(Header))
        {
            return new ProfileArchiveReading { Status = ProfileArchiveStatus.NotAnArchive };
        }

        var salt = archive.AsSpan(Header.Length, SaltLength).ToArray();
        var nonce = archive.AsSpan(Header.Length + SaltLength, NonceLength);
        var tag = archive.AsSpan(Header.Length + SaltLength + NonceLength, TagLength);
        var encrypted = archive.AsSpan(dataStart);
        var plain = new byte[encrypted.Length];
        var key = DeriveKey(password, salt);
        try
        {
            using var aes = new AesGcm(key, TagLength);
            aes.Decrypt(nonce, encrypted, tag, plain, Header);
            var values = JsonSerializer.Deserialize<List<ProfileValue>>(plain);
            return values is null
                ? new ProfileArchiveReading { Status = ProfileArchiveStatus.NotAnArchive }
                : new ProfileArchiveReading { Status = ProfileArchiveStatus.Read, Values = values };
        }
        catch (AuthenticationTagMismatchException)
        {
            return new ProfileArchiveReading { Status = ProfileArchiveStatus.WrongPassword };
        }
        catch (JsonException)
        {
            return new ProfileArchiveReading { Status = ProfileArchiveStatus.NotAnArchive };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyLength);
    }
}
