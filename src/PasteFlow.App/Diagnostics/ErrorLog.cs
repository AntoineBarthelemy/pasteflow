using System.Globalization;
using System.IO;

namespace PasteFlow.App.Diagnostics;

/// <summary>
/// Keeps the unexpected errors in a file next to the data of PasteFlow, so that a problem can be understood after the
/// fact instead of the application closing without a word.
/// </summary>
/// <remarks>
/// Only the kind of error and where it happened are written: never a value read on screen or from the profile.
/// </remarks>
internal static class ErrorLog
{
    private const long MaxBytes = 512 * 1024;

    /// <summary>
    /// Gets the file of the errors: %LOCALAPPDATA%\PasteFlow\erreurs.log.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "erreurs.log");

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
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{current.GetType().FullName} (0x{current.HResult:X8})"));
            lines.Add(current.StackTrace ?? "(pas de pile)");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
