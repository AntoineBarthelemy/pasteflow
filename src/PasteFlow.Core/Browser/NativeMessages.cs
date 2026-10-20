using System.Buffers.Binary;

namespace PasteFlow.Core.Browser;

/// <summary>
/// Reads and writes the messages of the native messaging of browsers: each message is a JSON text in UTF-8, preceded
/// by its length on 4 bytes, lowest byte first. PasteFlow uses the same framing between its own processes.
/// </summary>
public static class NativeMessages
{
    /// <summary>
    /// The largest message accepted, in bytes: a few pages of text, far more than what PasteFlow asks for.
    /// </summary>
    public const int MaxLength = 16 * 1024 * 1024;

    /// <summary>
    /// Reads the next message.
    /// </summary>
    /// <param name="stream">The stream to read.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The JSON text of the message, or <see langword="null"/> once the stream is closed.</returns>
    /// <exception cref="InvalidDataException">The length announced is not that of a message.</exception>
    public static async Task<string?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[4];
        if (!await FillAsync(stream, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 0 or > MaxLength)
        {
            throw new InvalidDataException("La longueur annoncée n'est pas celle d'un message.");
        }

        var body = new byte[length];
        return await FillAsync(stream, body, cancellationToken).ConfigureAwait(false)
            ? System.Text.Encoding.UTF8.GetString(body)
            : null;
    }

    /// <summary>
    /// Writes a message.
    /// </summary>
    /// <param name="stream">The stream to write.</param>
    /// <param name="json">The JSON text of the message.</param>
    /// <param name="cancellationToken">A token to cancel the writing.</param>
    /// <returns>A task completed once the message is written.</returns>
    /// <exception cref="ArgumentException">The message is too long.</exception>
    public static async Task WriteAsync(Stream stream, string json, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(json);

        var body = System.Text.Encoding.UTF8.GetBytes(json);
        if (body.Length > MaxLength)
        {
            throw new ArgumentException("Le message est trop long.", nameof(json));
        }

        var message = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(message, body.Length);
        body.CopyTo(message, 4);
        await stream.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> FillAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return false;
            }

            read += count;
        }

        return true;
    }
}
