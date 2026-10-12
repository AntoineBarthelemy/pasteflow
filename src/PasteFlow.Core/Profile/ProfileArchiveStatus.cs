namespace PasteFlow.Core.Profile;

/// <summary>
/// What came of the reading of an export of the profile.
/// </summary>
public enum ProfileArchiveStatus
{
    /// <summary>
    /// The export was read: its values are available.
    /// </summary>
    Read,

    /// <summary>
    /// The password is not the one of the export, or the export was changed since it was made.
    /// </summary>
    WrongPassword,

    /// <summary>
    /// The file is not an export of a PasteFlow profile.
    /// </summary>
    NotAnArchive,
}
