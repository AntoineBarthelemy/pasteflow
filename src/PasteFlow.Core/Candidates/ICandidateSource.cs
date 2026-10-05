namespace PasteFlow.Core.Candidates;

/// <summary>
/// A place where values to paste can be found: the screen, the profile of the user, the history, and so on.
/// </summary>
public interface ICandidateSource
{
    /// <summary>
    /// Gets the values currently offered by the source.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>
    /// The candidates found, or an empty list if the source is unavailable.
    /// Implementations never throw, except <see cref="OperationCanceledException"/>.
    /// </returns>
    Task<IReadOnlyList<Candidate>> GetCandidatesAsync(CancellationToken cancellationToken);
}
