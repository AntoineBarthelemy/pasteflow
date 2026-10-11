namespace PasteFlow.Core.Screen;

/// <summary>
/// Gives the text of the windows read for the last fill, to search in it.
/// </summary>
public interface IScreenTextSource
{
    /// <summary>
    /// Gets the windows read for the last fill, closest first.
    /// </summary>
    IReadOnlyList<WindowText> LastWindows { get; }
}
