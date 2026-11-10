namespace PasteFlow.Core.Picking;

/// <summary>
/// Puts a pasted value in the clipboard of Windows, so that Ctrl+V pastes it again and the history of Windows (Win+V)
/// keeps it, as if the user had copied it.
/// </summary>
public interface IClipboardWriter
{
    /// <summary>
    /// Puts a text in the clipboard.
    /// </summary>
    /// <param name="text">The text pasted at the cursor.</param>
    /// <param name="isSensitive">
    /// <see langword="true"/> for a sensitive value, such as an IBAN: Ctrl+V still pastes it, but it is kept out of the
    /// history of the clipboard and of its synchronization between devices.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the writing.</param>
    /// <returns><see langword="true"/> if the text is in the clipboard; otherwise <see langword="false"/>.</returns>
    Task<bool> CopyAsync(string text, bool isSensitive, CancellationToken cancellationToken);
}
