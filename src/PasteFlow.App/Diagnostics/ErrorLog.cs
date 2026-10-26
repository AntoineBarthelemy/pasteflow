using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace PasteFlow.App.Diagnostics;

/// <summary>
/// Keeps the unexpected errors in a file next to the data of PasteFlow, so that a problem can be understood after the
/// fact instead of the application closing without a word.
/// </summary>
/// <remarks>
/// Only the kind of error and where it happened are written: never a value read on screen or from the profile.
/// </remarks>
internal static partial class ErrorLog
{
    private const long MaxBytes = 512 * 1024;

    // Enough for the last errors, small enough to be pasted in a message
    private const int MaxCopiedLength = 30_000;
    private const int RegexTimeoutMilliseconds = 500;

    /// <summary>
    /// Gets the file of the errors: %LOCALAPPDATA%\PasteFlow\erreurs.log.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "erreurs.log");

    /// <summary>
    /// Gets a value indicating whether an error was written in the file.
    /// </summary>
    public static bool HasErrors
    {
        get
        {
            try
            {
                return File.Exists(DefaultPath) && new FileInfo(DefaultPath).Length > 0;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Opens the file of the errors in the Notepad of Windows.
    /// </summary>
    /// <returns><see langword="true"/> if the file was opened; otherwise <see langword="false"/>.</returns>
    public static bool Open()
    {
        if (!HasErrors)
        {
            return false;
        }

        try
        {
            // Notepad rather than the program tied to ".log", which may be none and ask the user to choose
            var start = new ProcessStartInfo("notepad.exe") { UseShellExecute = false };
            start.ArgumentList.Add(DefaultPath);
            using var notepad = Process.Start(start);
            return notepad is not null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the last errors of the file, to be copied and pasted in a message.
    /// </summary>
    /// <returns>The last errors, or an empty string if there is none.</returns>
    public static string ReadRecent()
    {
        try
        {
            return HasErrors ? Recent(File.ReadAllText(DefaultPath), MaxCopiedLength) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Adds an error to the file.
    /// </summary>
    /// <param name="where">What PasteFlow was doing.</param>
    /// <param name="exception">The error.</param>
    public static void Write(string where, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(where);
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            var path = DefaultPath;
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // The file never grows without end: past its size, it starts again
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
            {
                File.Delete(path);
            }

            var entry = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {where}{Environment.NewLine}{Describe(exception)}{Environment.NewLine}");
            File.AppendAllText(path, entry);
        }
        catch (Exception logging) when (logging is IOException or UnauthorizedAccessException)
        {
            // Without the file, the error is simply not kept
        }
    }

    /// <summary>
    /// Describes an error without its message, which may quote a value: its type and where it happened.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <returns>The description, inner errors included.</returns>
    internal static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var lines = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{current.GetType().FullName} (0x{current.HResult:X8}){NetworkDetail(current)}"));
            lines.Add(current.StackTrace ?? "(pas de pile)");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Tells why a network request failed, without its address: the answer of the server or the kind of failure.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <returns>A detail such as " HTTP 403", or an empty string.</returns>
    internal static string NetworkDetail(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            HttpRequestException { StatusCode: { } status } => string.Create(CultureInfo.InvariantCulture, $" HTTP {(int)status}"),
            HttpRequestException request => $" {request.HttpRequestError}",
            SocketException socket => $" {socket.SocketErrorCode}",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Keeps the end of the text of the file, starting at the beginning of an error.
    /// </summary>
    /// <param name="text">The whole text of the file.</param>
    /// <param name="maxLength">The longest text kept.</param>
    /// <returns>The last whole errors that fit.</returns>
    internal static string Recent(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= maxLength)
        {
            return text;
        }

        // Every error starts with its date at the beginning of a line: the cut lands on one
        var tail = text[^maxLength..];
        try
        {
            var start = EntryStart().Match(tail);
            return start.Success ? tail[start.Index..] : tail;
        }
        catch (RegexMatchTimeoutException)
        {
            return tail;
        }
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} ", RegexOptions.Multiline | RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex EntryStart();
}
