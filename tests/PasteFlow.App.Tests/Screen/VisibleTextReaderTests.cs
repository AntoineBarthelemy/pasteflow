using PasteFlow.App.Screen;
using PasteFlow.App.Tests.TestWindows;

namespace PasteFlow.App.Tests.Screen;

public sealed class VisibleTextReaderTests
{
    [Fact]
    public void ReadWindow_ReadsARealWindowWithoutFailing()
    {
        using var window = new WindowHost<ContactFormWindow>(() => new ContactFormWindow());

        // Whether the texts are seen depends on the windows above: reading must simply never fail
        var texts = VisibleTextReader.ReadWindow(window.Handle);

        Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text.Text)));
    }
}
