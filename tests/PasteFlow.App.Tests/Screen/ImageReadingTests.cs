using PasteFlow.App.Screen;
using PasteFlow.App.Tests.TestWindows;

namespace PasteFlow.App.Tests.Screen;

public sealed class ImageReadingTests : IDisposable
{
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(5);

    private readonly WindowHost<ImageTextWindow> _imageWindow = new(() => new ImageTextWindow());
    private readonly OcrTextRecognizer? _ocr = OcrTextRecognizer.TryCreate();

    [Fact]
    public void TryCapturePng_TakesAPictureOfTheWindow()
    {
        var png = WindowCapture.TryCapturePng(_imageWindow.Handle);

        Assert.NotNull(png);
        Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, png.Take(4));
    }

    [Fact]
    public void TryCapturePng_WithoutWindow_ReturnsNull()
    {
        Assert.Null(WindowCapture.TryCapturePng(IntPtr.Zero));
    }

    [SkippableFact]
    public async Task RecognizeAsync_ReadsTheTextOfAPicture()
    {
        Skip.If(_ocr is null, "Windows has no character recognition for the languages of this computer.");

        // The window draws its picture a moment after it opens: the capture is taken again until the text shows
        string text;
        var deadline = DateTime.UtcNow + RenderTimeout;
        do
        {
            var png = WindowCapture.TryCapturePng(_imageWindow.Handle);
            Assert.NotNull(png);
            text = await _ocr.RecognizeAsync(png, CancellationToken.None);
            if (!text.Contains("48213", StringComparison.Ordinal))
            {
                await Task.Delay(300);
            }
        }
        while (!text.Contains("48213", StringComparison.Ordinal) && DateTime.UtcNow < deadline);

        Assert.Contains("48213", text, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task ReadWithFallbackAsync_ReadsTheImageOfAWindowGivingNoText()
    {
        Skip.If(_ocr is null, "Windows has no character recognition for the languages of this computer.");

        // The window draws its picture a moment after it opens, later still on a busy machine: read again until it shows
        var reader = new UiAutomationWindowTextReader(_ocr);
        var windowText = await reader.ReadWithFallbackAsync(_imageWindow.Handle, rank: 1, CancellationToken.None);
        var deadline = DateTime.UtcNow + RenderTimeout;
        while (!(windowText?.Text.Contains("48213", StringComparison.Ordinal) ?? false) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(300);
            windowText = await reader.ReadWithFallbackAsync(_imageWindow.Handle, rank: 1, CancellationToken.None);
        }

        Assert.NotNull(windowText);
        Assert.True(windowText.IsReadFromImage);
        Assert.Contains("48213", windowText.Text, StringComparison.Ordinal);
        Assert.Equal("Document scanné", windowText.Title);
    }

    [Fact]
    public async Task ReadWithFallbackAsync_NeverReadsTheImageOfTheFormItself()
    {
        var windowText = await new UiAutomationWindowTextReader(_ocr).ReadWithFallbackAsync(_imageWindow.Handle, rank: 0, CancellationToken.None);

        Assert.False(windowText?.IsReadFromImage ?? false);
    }

    [Fact]
    public async Task ReadWithFallbackAsync_WhenReadingImagesIsOff_ReadsNoImage()
    {
        var reader = new UiAutomationWindowTextReader(_ocr) { ReadImages = false };

        var windowText = await reader.ReadWithFallbackAsync(_imageWindow.Handle, rank: 1, CancellationToken.None);

        Assert.False(windowText?.IsReadFromImage ?? false);
    }

    [Fact]
    public async Task ReadWithFallbackAsync_ReadsNoImageOfAWindowGivingItsText()
    {
        using var formWindow = new WindowHost<ContactFormWindow>(() => new ContactFormWindow());

        var windowText = await new UiAutomationWindowTextReader(_ocr).ReadWithFallbackAsync(formWindow.Handle, rank: 1, CancellationToken.None);

        Assert.NotNull(windowText);
        Assert.False(windowText.IsReadFromImage);
        Assert.Contains(ContactFormWindow.OrderText, windowText.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadWithFallbackAsync_WithoutCharacterRecognition_ReadsNoImage()
    {
        var windowText = await new UiAutomationWindowTextReader().ReadWithFallbackAsync(_imageWindow.Handle, rank: 1, CancellationToken.None);

        Assert.False(windowText?.IsReadFromImage ?? false);
    }

    public void Dispose()
    {
        _imageWindow.Dispose();
    }
}
