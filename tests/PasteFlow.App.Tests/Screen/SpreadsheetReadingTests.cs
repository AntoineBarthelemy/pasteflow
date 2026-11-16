using PasteFlow.App.Screen;
using PasteFlow.App.Tests.TestWindows;

namespace PasteFlow.App.Tests.Screen;

/// <summary>
/// A cell narrower than its text is read whole, row by row (seen in Excel on 14/11/2026: the screen showed
/// « helene.more », and the window was read from its image).
/// </summary>
public sealed class SpreadsheetReadingTests : IDisposable
{
    private readonly WindowHost<SpreadsheetWindow> _host = new(() => new SpreadsheetWindow());

    [Fact]
    public void ReadWindow_ReadsEachCellWholeRowByRow()
    {
        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 1);

        Assert.NotNull(windowText);
        Assert.Contains($"{SpreadsheetWindow.Order}\t{SpreadsheetWindow.Email}", windowText.Text, StringComparison.Ordinal);
    }

    public void Dispose() => _host.Dispose();
}
