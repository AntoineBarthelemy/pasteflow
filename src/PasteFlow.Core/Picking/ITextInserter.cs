namespace PasteFlow.Core.Picking;

/// <summary>
/// Writes text where the user's text cursor is, as if typed or pasted, without replacing what is already written.
/// </summary>
public interface ITextInserter
{
    /// <summary>
    /// Writes a text at the cursor of the active window.
    /// </summary>
    /// <param name="text">The text to write.</param>
    /// <param name="cancellationToken">A token to cancel the writing.</param>
    /// <returns><see langword="true"/> if the text was written; otherwise <see langword="false"/>.</returns>
    Task<bool> InsertAsync(string text, CancellationToken cancellationToken);
}
