using System.Windows;

namespace PasteFlow.App.Forms;

/// <summary>
/// Where a field is on the screen and whether it holds a value, read at a given moment.
/// </summary>
/// <param name="Bounds">The position of the field in screen pixels; <see cref="Rect.Empty"/> when it is not visible.</param>
/// <param name="HasValue">A value indicating whether the field holds a value.</param>
internal readonly record struct FieldState(Rect Bounds, bool HasValue);
