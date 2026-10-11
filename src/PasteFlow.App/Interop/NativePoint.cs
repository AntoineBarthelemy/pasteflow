using System.Runtime.InteropServices;

namespace PasteFlow.App.Interop;

/// <summary>
/// A point in pixels, as Win32 gives it.
/// </summary>
/// <param name="X">The horizontal position.</param>
/// <param name="Y">The vertical position.</param>
[StructLayout(LayoutKind.Sequential)]
internal record struct NativePoint(int X, int Y);
