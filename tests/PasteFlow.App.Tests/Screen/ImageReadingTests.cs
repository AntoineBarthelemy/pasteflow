using PasteFlow.App.Screen;
using PasteFlow.App.Tests.TestWindows;

namespace PasteFlow.App.Tests.Screen;

public sealed class ImageReadingTests : IDisposable
{
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
        var png = WindowCapture.TryCapturePng(_imageWindow.Handle);
        Assert.NotNull(png);

        var text = await _ocr.RecognizeAsync(png, CancellationToken.None);

        Assert.Contains("48213", text, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task ReadWithFallbackAsync_ReadsTheImageOfAWindowGivingNoText()
    {
        Skip.If(_ocr is null, "Windows has no character recognition for the languages of this computer.");

        var windowText = await new UiAutomationWindowTextReader(_ocr).ReadWithFallbackAsync(_imageWindow.Handle, rank: 1, CancellationToken.None);

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
