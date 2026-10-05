using PasteFlow.App.Screen;
using PasteFlow.App.Tests.TestWindows;

namespace PasteFlow.App.Tests.Screen;

public sealed class UiAutomationWindowTextReaderTests : IDisposable
{
    private readonly WindowHost<ContactFormWindow> _host = new(() => new ContactFormWindow());

    [Fact]
    public void ReadWindow_ReadsTitleAndRank()
    {
        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 2);

        Assert.NotNull(windowText);
        Assert.Equal(ContactFormWindow.WindowTitle, windowText.Title);
        Assert.Equal(2, windowText.Rank);
    }

    [Fact]
    public void ReadWindow_ReadsDisplayedText()
    {
        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 0);

        Assert.NotNull(windowText);
        Assert.Contains(ContactFormWindow.OrderText, windowText.Text, StringComparison.Ordinal);
        Assert.Contains(ContactFormWindow.TotalText, windowText.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadWindow_NeverReadsPasswords()
    {
        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 0);

        Assert.NotNull(windowText);
        Assert.DoesNotContain(ContactFormWindow.PasswordValue, windowText.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadWindow_WithClosedWindow_ReturnsNull()
    {
        var handle = _host.Handle;
        _host.Dispose();

        var windowText = UiAutomationWindowTextReader.ReadWindow(handle, rank: 0);

        Assert.Null(windowText);
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}
