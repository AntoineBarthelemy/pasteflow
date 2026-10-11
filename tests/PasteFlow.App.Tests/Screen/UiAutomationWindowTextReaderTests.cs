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

    [Fact]
    public void ReadWindow_BehindTheForm_ReadsFilledFieldsWithTheirLabel()
    {
        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 1);

        Assert.NotNull(windowText);
        var email = Assert.Single(windowText.Fields);
        Assert.Equal("Email", email.Label);
        Assert.Equal("antoine@example.com", email.Value);
    }

    [Fact]
    public void ReadWindow_BehindTheForm_KeepsLineBreaksOfValues()
    {
        const string Description = "Première ligne\nDeuxième ligne";
        _ = _host.Invoke(window =>
        {
            window.NameBox.AcceptsReturn = true;
            window.NameBox.Text = Description;
            return true;
        });

        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 1);

        Assert.NotNull(windowText);
        var name = Assert.Single(windowText.Fields, field => field.Label == "Nom");
        Assert.Equal(Description, name.Value.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void ReadWindow_BehindTheForm_NeverReadsSearchBoxes()
    {
        _ = _host.Invoke(window => window.SearchBox.Text = "Taissy");

        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 1);

        Assert.NotNull(windowText);
        Assert.DoesNotContain(windowText.Fields, field => field.Value == "Taissy");
    }

    [Fact]
    public void ReadWindow_BehindTheForm_NeverReadsPasswordFields()
    {
        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 1);

        Assert.NotNull(windowText);
        Assert.DoesNotContain(windowText.Fields, field => field.Value.Contains(ContactFormWindow.PasswordValue, StringComparison.Ordinal));
        Assert.DoesNotContain(windowText.Fields, field => field.Label == "Mot de passe");
    }

    [Fact]
    public void ReadWindow_OfTheFormItself_ReadsNoFields()
    {
        var windowText = UiAutomationWindowTextReader.ReadWindow(_host.Handle, rank: 0);

        Assert.NotNull(windowText);
        Assert.Empty(windowText.Fields);
    }

    public void Dispose()
    {
        _host.Dispose();
    }
}
