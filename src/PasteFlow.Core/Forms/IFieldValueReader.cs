namespace PasteFlow.Core.Forms;

/// <summary>
/// Reads the current value of fields returned by a recent reading of an <see cref="IFormReader"/>.
/// </summary>
public interface IFieldValueReader
{
    /// <summary>
    /// Reads the current value of a field.
    /// </summary>
    /// <param name="field">The field, as returned by a recent reading of the form.</param>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>
    /// The current value, or <see langword="null"/> if the field cannot be read anymore (window closed, field removed)
    /// or must not be read (password). Implementations never throw, except <see cref="OperationCanceledException"/>.
    /// </returns>
    Task<string?> TryReadValueAsync(FormField field, CancellationToken cancellationToken);
}
