using PasteFlow.App.Profile;

namespace PasteFlow.App.Tests.Fakes;

/// <summary>
/// Answers the questions of an export or an import as the test decided, without any window.
/// </summary>
internal sealed class FakeProfileTransferPrompts : IProfileTransferPrompts
{
    public string? ExportPath { get; set; }

    public string? ImportPath { get; set; }

    public string? Password { get; set; }

    public int PasswordQuestions { get; private set; }

    public string? ChooseExportPath()
    {
        return ExportPath;
    }

    public string? ChooseImportPath()
    {
        return ImportPath;
    }

    public string? AskPassword(bool isNew)
    {
        PasswordQuestions++;
        return Password;
    }
}
