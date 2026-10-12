using System.Windows;
using Microsoft.Win32;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Profile;

/// <summary>
/// Asks where to export or import the profile with the usual Windows file dialogs.
/// </summary>
internal sealed class WindowsProfileTransferPrompts : IProfileTransferPrompts
{
    private const string Filter = "Export de fiche PasteFlow (*" + ProfileArchive.FileExtension + ")|*" + ProfileArchive.FileExtension;

    private readonly Window _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsProfileTransferPrompts"/> class.
    /// </summary>
    /// <param name="owner">The window the dialogs belong to.</param>
    public WindowsProfileTransferPrompts(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        _owner = owner;
    }

    /// <inheritdoc/>
    public string? ChooseExportPath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exporter ma fiche",
            Filter = Filter,
            FileName = "ma-fiche" + ProfileArchive.FileExtension,
            AddExtension = true,
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? ChooseImportPath()
    {
        var dialog = new OpenFileDialog { Title = "Importer une fiche", Filter = Filter };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? AskPassword(bool isNew)
    {
        var prompt = new PasswordPromptWindow(isNew) { Owner = _owner };
        return prompt.ShowDialog() == true ? prompt.Password : null;
    }
}
