using System.Runtime.InteropServices;

namespace PasteFlow.App.Interop;

/// <summary>
/// A rectangle in screen pixels, as Win32 functions give it.
/// </summary>
/// <param name="Left">The left side.</param>
/// <param name="Top">The top side.</param>
/// <param name="Right">The right side, excluded.</param>
/// <param name="Bottom">The bottom side, excluded.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct NativeRect(int Left, int Top, int Right, int Bottom)
{
    /// <summary>
    /// Gets the width of the rectangle.
    /// </summary>
    public int Width => Right - Left;

    /// <summary>
    /// Gets the height of the rectangle.
    /// </summary>
    public int Height => Bottom - Top;
}
