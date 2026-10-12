using System.Diagnostics;

namespace PasteFlow.App.Screen;

/// <summary>
/// Tells which program a window belongs to, from the program really running rather than from its title.
/// </summary>
internal static class WindowProgram
{
    /// <summary>
    /// Gets the name of the program of a process, such as "steam" or "chrome".
    /// </summary>
    /// <param name="processId">The identifier of the process of the window.</param>
    /// <returns>The name of the program, in lower case, or an empty string if it cannot be read.</returns>
    public static string NameOf(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName.ToLowerInvariant();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // The process ended meanwhile
            return string.Empty;
        }
    }
}
