namespace PasteFlow.Core.Forms;

/// <summary>
/// Writes values into the fields returned by the last reading of an <see cref="IFormReader"/>.
/// </summary>
public interface IFieldWriter
{
    /// <summary>
    /// Writes a value into a field, replacing its content.
    /// </summary>
    /// <param name="field">The field, as returned by the last reading of the form.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="cancellationToken">A token to cancel the writing.</param>
    /// <returns>
    /// <see langword="true"/> if the value was written; <see langword="false"/> if the field cannot be written.
    /// Implementations never throw, except <see cref="OperationCanceledException"/>.
    /// </returns>
    Task<bool> TryWriteAsync(FormField field, string value, CancellationToken cancellationToken);
}
