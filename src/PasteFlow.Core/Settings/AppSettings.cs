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
    /// Gets a value indicating whether the application starts with Windows; off by default.
    /// </summary>
    public bool StartWithWindows { get; init; }
}
