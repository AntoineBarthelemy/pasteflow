namespace PasteFlow.Core.Profile;

/// <summary>
/// What an import brought to the profile.
/// </summary>
public sealed record ProfileImportSummary
{
    /// <summary>
    /// Gets the number of values added; the values already in the profile are not counted.
    /// </summary>
    public required int Added { get; init; }

    /// <summary>
    /// Gets a value indicating whether the profile could be saved after the import.
    /// </summary>
    public required bool IsSaved { get; init; }
}
