using System.Windows;
using System.Windows.Threading;
using PasteFlow.App.Picking;
using PasteFlow.App.Screen;

namespace PasteFlow.App.Tests.Screen;

public sealed class ClipboardWatcherTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsKept_ATextCopiedByTheUser_IsKept()
    {
        Assert.True(ClipboardWatcher.IsKept(new[] { DataFormats.UnicodeText, DataFormats.Text, "Rich Text Format" }));
    }

    [Theory]
    [InlineData("ExcludeClipboardContentFromMonitorProcessing")]
    [InlineData("CanIncludeInClipboardHistory")]
    [InlineData("Clipboard Viewer Ignore")]
    public void IsKept_ACopyMarkedSensitive_IsNeverKept(string format)
    {
        // A password manager marks its copies this way
        Assert.False(ClipboardWatcher.IsKept(new[] { DataFormats.UnicodeText, format }));
    }

    [Fact]
    public void IsKept_ACopyOfPasteFlow_IsNotACopyOfTheUser()
    {
        var formats = WindowsClipboard.DataFor("06 12 48 30 77", isSensitive: false).GetFormats();

        Assert.False(ClipboardWatcher.IsKept(formats));
    }

    [Fact]
    public void IsKept_WithoutText_IsNotKept()
    {
        Assert.False(ClipboardWatcher.IsKept(new[] { DataFormats.Bitmap }));
    }

    [Fact]
    public void RecentAt_ACopyOfAMomentAgo_IsGiven()
    {
        var copy = new ClipboardCopy { Text = "CMD-4471", CopiedAt = Now - TimeSpan.FromMinutes(3) };

        Assert.Same(copy, ClipboardWatcher.RecentAt(copy, Now));
    }

    [Fact]
    public void RecentAt_ACopyForgottenLongAgo_IsNoSource()
    {
        // A text copied an hour ago must not fill a form
        var copy = new ClipboardCopy { Text = "CMD-4471", CopiedAt = Now - ClipboardWatcher.MaxAge - TimeSpan.FromSeconds(1) };

        Assert.Null(ClipboardWatcher.RecentAt(copy, Now));
        Assert.Null(ClipboardWatcher.RecentAt(null, Now));
    }

    [Fact]
    public void Recent_AfterARealCopy_GivesItsText_AndNotTheCopiesOfPasteFlow()
    {
        string? copied = null;
        string? afterOwnCopy = "pas lu";
        var thread = new Thread(() =>
        {
            using var watcher = new ClipboardWatcher();
            Clipboard.SetText("Commande : CMD-4471, helene.moreau@moreau-conseil.fr");
            copied = WaitFor(() => watcher.Recent()?.Text);

            Clipboard.SetDataObject(WindowsClipboard.DataFor("0612483077", isSensitive: false), copy: true);
            Pump(TimeSpan.FromSeconds(1));
            afterOwnCopy = watcher.Recent()?.Text;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));

        Assert.Equal("Commande : CMD-4471, helene.moreau@moreau-conseil.fr", copied);
        Assert.Null(afterOwnCopy);
    }

    private static string? WaitFor(Func<string?> read)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        string? value = null;
        while (value is null && DateTime.UtcNow < deadline)
        {
            Pump(TimeSpan.FromMilliseconds(100));
            value = read();
        }

        return value;
    }

    // Lets the window of the watcher receive the message of Windows
    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(duration, DispatcherPriority.Background, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }
}
