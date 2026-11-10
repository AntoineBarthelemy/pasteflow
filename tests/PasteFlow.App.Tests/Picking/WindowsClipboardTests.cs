using System.Windows;
using PasteFlow.App.Picking;

namespace PasteFlow.App.Tests.Picking;

public sealed class WindowsClipboardTests
{
    private static readonly string[] HistoryFormats =
    {
        "ExcludeClipboardContentFromMonitorProcessing",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard",
    };

    [Fact]
    public void DataFor_AnOrdinaryValue_IsPlainTextKeptInTheHistory()
    {
        var data = WindowsClipboard.DataFor("06 12 48 30 77", isSensitive: false);

        Assert.Equal("06 12 48 30 77", data.GetText(TextDataFormat.UnicodeText));
        Assert.All(HistoryFormats, format => Assert.False(data.GetDataPresent(format)));
    }

    [Fact]
    public void DataFor_ASensitiveValue_IsKeptOutOfTheHistoryAndTheCloud()
    {
        var data = WindowsClipboard.DataFor("FR76 3000 6000 0112 3456 7890 189", isSensitive: true);

        Assert.Equal("FR76 3000 6000 0112 3456 7890 189", data.GetText(TextDataFormat.UnicodeText));
        Assert.All(HistoryFormats, format => Assert.True(data.GetDataPresent(format)));
    }
}
