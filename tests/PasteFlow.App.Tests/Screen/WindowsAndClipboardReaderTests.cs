using PasteFlow.App.Screen;
using PasteFlow.Core.Screen;

namespace PasteFlow.App.Tests.Screen;

public sealed class WindowsAndClipboardReaderTests
{
    private static readonly WindowText Form = new() { Title = "Nouveau client", Text = "Nom\nE-mail", Rank = 0 };

    [Fact]
    public async Task ReadWindowsAsync_WithARecentCopy_AddsItRightBehindTheForm()
    {
        var copy = new ClipboardCopy { Text = "helene.moreau@moreau-conseil.fr", CopiedAt = DateTime.UtcNow };
        var reader = new WindowsAndClipboardReader(new FixedReader(Form), () => copy);

        var windows = await reader.ReadWindowsAsync(CancellationToken.None);

        Assert.Equal(2, windows.Count);
        var copied = windows[1];
        Assert.Equal(1, copied.Rank);
        Assert.Equal("helene.moreau@moreau-conseil.fr", copied.Text);
        Assert.StartsWith(WindowsAndClipboardReader.SourceName, copied.Title, StringComparison.Ordinal);
        Assert.False(copied.IsReadFromImage);
    }

    [Fact]
    public async Task ReadWindowsAsync_WithoutRecentCopy_GivesTheWindowsOnly()
    {
        var reader = new WindowsAndClipboardReader(new FixedReader(Form), () => null);

        var windows = await reader.ReadWindowsAsync(CancellationToken.None);

        Assert.Same(Form, Assert.Single(windows));
    }

    private sealed class FixedReader : IWindowTextReader
    {
        private readonly WindowText[] _windows;

        public FixedReader(params WindowText[] windows)
        {
            _windows = windows;
        }

        public Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WindowText>>(_windows);
        }
    }
}
