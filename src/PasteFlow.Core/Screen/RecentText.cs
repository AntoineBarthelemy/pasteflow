namespace PasteFlow.Core.Screen;

/// <summary>
/// A text kept by the memory of the screen.
/// </summary>
/// <param name="Window">The window as it was read.</param>
/// <param name="SeenAt">When it was read.</param>
public sealed record RecentText(WindowText Window, DateTimeOffset SeenAt);
