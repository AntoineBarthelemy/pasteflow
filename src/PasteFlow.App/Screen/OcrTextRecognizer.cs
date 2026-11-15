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
    private const double UpScale = 2;
    private const int RedFloor = 140;
    private const int RedMargin = 70;

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
        using var bitmap = await DecodeAsync(decoder, 1, cancellationToken);
        var lines = (await _engine.RecognizeAsync(bitmap).AsTask(cancellationToken)).Lines.Select(line => line.Text).ToList();

        // The text of a window on a screen is small, about 15 pixels high, and the red wavy line of a spell checker
        // breaks the words it underlines: in LibreOffice, « SMp-4471 » was read for « CMD-4471 » and the e-mail was
        // lost (15/11/2026). The picture is read once more, larger and without its red, and what this second reading
        // finds is added: the first reading is kept as it is, since larger letters sometimes lose a character (a scan
        // read « CMD-7712 » for « CMD-77120 »). Values read in a picture are only proposed, never written alone
        using var larger = await DecodeAsync(decoder, UpScale, cancellationToken);
        var pixels = new byte[4 * larger.PixelWidth * larger.PixelHeight];
        larger.CopyToBuffer(pixels.AsBuffer());
        _ = WipeRed(pixels);
        using var cleaned = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), larger.BitmapPixelFormat, larger.PixelWidth, larger.PixelHeight, larger.BitmapAlphaMode);
        var second = await _engine.RecognizeAsync(cleaned).AsTask(cancellationToken);
        lines.AddRange(second.Lines.Select(line => line.Text).Where(text => !lines.Contains(text, StringComparer.Ordinal)).ToList());
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Turns the red pixels of a picture white, as the wavy lines of a spell checker.
    /// </summary>
    /// <param name="bgra">The pixels, four bytes each in the order blue, green, red, alpha, changed in place.</param>
    /// <returns><see langword="true"/> if the picture had red pixels; otherwise <see langword="false"/>.</returns>
    internal static bool WipeRed(byte[] bgra)
    {
        ArgumentNullException.ThrowIfNull(bgra);

        var found = false;
        for (var index = 0; index + 3 < bgra.Length; index += 4)
        {
            int blue = bgra[index], green = bgra[index + 1], red = bgra[index + 2];
            if (red > RedFloor && red - green > RedMargin && red - blue > RedMargin)
            {
                bgra[index] = bgra[index + 1] = bgra[index + 2] = byte.MaxValue;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Decodes the picture at a scale, made smaller when it would be larger than what the recognizer accepts.
    /// </summary>
    /// <param name="decoder">The decoder of the picture.</param>
    /// <param name="wantedScale">The scale wanted, 1 for the picture as it is.</param>
    /// <param name="cancellationToken">A token to cancel the decoding.</param>
    /// <returns>The picture, ready to be read.</returns>
    private static async Task<SoftwareBitmap> DecodeAsync(BitmapDecoder decoder, double wantedScale, CancellationToken cancellationToken)
    {
        var maxSide = OcrEngine.MaxImageDimension;
        var largestSide = Math.Max(decoder.PixelWidth, decoder.PixelHeight);

        var scale = Math.Min(wantedScale, (double)maxSide / largestSide);
        var transform = new BitmapTransform { InterpolationMode = BitmapInterpolationMode.Cubic };
        if (Math.Abs(scale - 1) > 0.01)
        {
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
