namespace PasteFlow.Core.Forms;

/// <summary>
/// Reads the fields of the form displayed in the active window.
/// </summary>
public interface IFormReader
{
    /// <summary>
    /// Reads the visible and enabled fields of the active window.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>
    /// The fields in reading order, or an empty list if the window cannot be read.
    /// Implementations never throw, except <see cref="OperationCanceledException"/>.
    /// </returns>
    Task<IReadOnlyList<FormField>> ReadActiveFormAsync(CancellationToken cancellationToken);
}
