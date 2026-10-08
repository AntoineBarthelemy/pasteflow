using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PasteFlow.App.Screen;

/// <summary>
/// Reads the text of a picture with the character recognition built into Windows (OCR), in the languages of the user.
/// </summary>
internal sealed class OcrTextRecognizer
{
    private readonly OcrEngine _engine;

    private OcrTextRecognizer(OcrEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Creates a recognizer for the languages of the user.
    /// </summary>
    /// <returns>The recognizer, or <see langword="null"/> when Windows has no character recognition for these languages.</returns>
    public static OcrTextRecognizer? TryCreate()
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        return engine is null ? null : new OcrTextRecognizer(engine);
    }

    /// <summary>
    /// Reads the text of a picture.
    /// </summary>
    /// <param name="png">The picture, as PNG bytes.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The lines of text read, separated by line breaks.</returns>
    public async Task<string> RecognizeAsync(byte[] png, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(png);

        using var stream = new InMemoryRandomAccessStream();
        _ = await stream.WriteAsync(png.AsBuffer()).AsTask(cancellationToken);
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        using var bitmap = await DecodeAsync(decoder, cancellationToken);
        var result = await _engine.RecognizeAsync(bitmap).AsTask(cancellationToken);
        return string.Join('\n', result.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// Decodes the picture, made smaller when it is larger than what the recognizer accepts.
    /// </summary>
    /// <param name="decoder">The decoder of the picture.</param>
    /// <param name="cancellationToken">A token to cancel the decoding.</param>
    /// <returns>The picture, ready to be read.</returns>
    private static async Task<SoftwareBitmap> DecodeAsync(BitmapDecoder decoder, CancellationToken cancellationToken)
    {
        var maxSide = OcrEngine.MaxImageDimension;
        var largestSide = Math.Max(decoder.PixelWidth, decoder.PixelHeight);
        var transform = new BitmapTransform();
        if (largestSide > maxSide)
        {
            var scale = (double)maxSide / largestSide;
            transform.ScaledWidth = (uint)(decoder.PixelWidth * scale);
            transform.ScaledHeight = (uint)(decoder.PixelHeight * scale);
        }

        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(cancellationToken);
    }
}
