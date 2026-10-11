namespace PasteFlow.Core.Picking;

/// <summary>
/// Lets the user choose what to paste when nothing is sure: a short list, and a search in the windows read.
/// </summary>
public interface IPastePicker
{
    /// <summary>
    /// Shows the choices next to the cursor and waits for the user.
    /// </summary>
    /// <param name="request">The values to offer and the search in the windows.</param>
    /// <param name="cancellationToken">A token to cancel the choice.</param>
    /// <returns>The text chosen, or <see langword="null"/> if the user cancelled.</returns>
    Task<string?> PickAsync(PickRequest request, CancellationToken cancellationToken);
}
