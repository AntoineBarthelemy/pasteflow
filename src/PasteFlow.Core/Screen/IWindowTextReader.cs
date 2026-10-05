namespace PasteFlow.Core.Screen;

/// <summary>
/// Reads the text displayed in the active window and in the windows just behind it.
/// </summary>
public interface IWindowTextReader
{
    /// <summary>
    /// Reads the text of the active window and of the windows just behind it, in Alt+Tab order.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>
    /// The text of each window that could be read in time. Windows that do not answer are skipped.
    /// Implementations never throw, except <see cref="OperationCanceledException"/>.
    /// </returns>
    Task<IReadOnlyList<WindowText>> ReadWindowsAsync(CancellationToken cancellationToken);
}
