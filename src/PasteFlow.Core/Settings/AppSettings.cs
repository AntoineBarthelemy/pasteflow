using PasteFlow.Core.Matching;

namespace PasteFlow.Core.Settings;

/// <summary>
/// The choices of the user.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// Gets the settings of a first launch.
    /// </summary>
    public static AppSettings Default { get; } = new();

    /// <summary>
    /// Gets how sure a value must be to fill a field without asking; balanced by default.
    /// </summary>
    public Prudence Prudence { get; init; } = Prudence.Balanced;

    /// <summary>
    /// Gets a value indicating whether the application learns from the fills, in its encrypted history; on by default.
    /// </summary>
    public bool LearnFromFills { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the text of windows that give none is read from their image (OCR); on by default.
    /// The values read this way are only proposed.
    /// </summary>
    public bool ReadImages { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the texts seen on screen are kept 30 minutes in memory, never on the disk, to be
    /// found again in the list of Ctrl+Alt+V; on by default (decision of 14/11/2026).
    /// </summary>
    public bool RememberScreen { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the application starts with Windows; off by default.
    /// </summary>
    public bool StartWithWindows { get; init; }
}
