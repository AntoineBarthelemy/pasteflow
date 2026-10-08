using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PasteFlow.App.Interop;

namespace PasteFlow.App.Screen;

/// <summary>
/// Takes a picture of a window, even when other windows cover it.
/// </summary>
internal static class WindowCapture
{
    // Also draws the content of windows rendered by the GPU, such as browsers
    private const uint PrintWindowRenderFullContent = 2;
    private const int MaxSide = 10_000;

    /// <summary>
    /// Takes a picture of a window.
    /// </summary>
    /// <param name="handle">The window.</param>
    /// <returns>The picture as PNG bytes, or <see langword="null"/> if the window cannot be drawn.</returns>
    public static byte[]? TryCapturePng(IntPtr handle)
    {
        if (!NativeMethods.GetWindowRect(handle, out var bounds)
            || bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > MaxSide || bounds.Height > MaxSide)
        {
            return null;
        }

        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var deviceContext = graphics.GetHdc();
            try
            {
                if (!NativeMethods.PrintWindow(handle, deviceContext, PrintWindowRenderFullContent))
                {
                    return null;
                }
            }
            finally
            {
                graphics.ReleaseHdc(deviceContext);
            }
        }

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        return png.ToArray();
    }
}
