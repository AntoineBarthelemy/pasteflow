namespace PasteFlow.App.Profile;

/// <summary>
/// Asks the user where to export or import their profile, and the password that protects the export.
/// </summary>
internal interface IProfileTransferPrompts
{
    /// <summary>
    /// Asks where to save an export of the profile.
    /// </summary>
    /// <returns>The path chosen, or <see langword="null"/> if the user cancelled.</returns>
    string? ChooseExportPath();

    /// <summary>
    /// Asks which export of the profile to import.
    /// </summary>
    /// <returns>The path chosen, or <see langword="null"/> if the user cancelled.</returns>
    string? ChooseImportPath();

    /// <summary>
    /// Asks the password of an export.
    /// </summary>
    /// <param name="isNew">
    /// <see langword="true"/> for a new export, where the password is typed twice;
    /// <see langword="false"/> to open an existing export.
    /// </param>
    /// <returns>The password, or <see langword="null"/> if the user cancelled.</returns>
    string? AskPassword(bool isNew);
}
