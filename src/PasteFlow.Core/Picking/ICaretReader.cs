namespace PasteFlow.Core.Picking;

/// <summary>
/// Reads where the user's text cursor is.
/// </summary>
public interface ICaretReader
{
    /// <summary>
    /// Reads the place holding the text cursor in the active window.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The place of the cursor, or <see langword="null"/> when it cannot be read.</returns>
    Task<CaretContext?> ReadCaretAsync(CancellationToken cancellationToken);
}
