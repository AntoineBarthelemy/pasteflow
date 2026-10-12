namespace PasteFlow.Core.Profile;

/// <summary>
/// The result of the reading of an export of the profile.
/// </summary>
public sealed record ProfileArchiveReading
{
    /// <summary>
    /// Gets what came of the reading.
    /// </summary>
    public required ProfileArchiveStatus Status { get; init; }

    /// <summary>
    /// Gets the values of the export; empty unless <see cref="Status"/> is <see cref="ProfileArchiveStatus.Read"/>.
    /// </summary>
    public IReadOnlyList<ProfileValue> Values { get; init; } = Array.Empty<ProfileValue>();
}
