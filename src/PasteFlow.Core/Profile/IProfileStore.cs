namespace PasteFlow.Core.Profile;

/// <summary>
/// Keeps the profile of the user between two launches of the application.
/// </summary>
public interface IProfileStore
{
    /// <summary>
    /// Reads the profile saved.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the reading.</param>
    /// <returns>The values saved; empty if there are none or they cannot be read. Never throws, except on cancellation.</returns>
    Task<IReadOnlyList<ProfileValue>> LoadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the profile, replacing what was saved before.
    /// </summary>
    /// <param name="values">The values to save.</param>
    /// <param name="cancellationToken">A token to cancel the saving.</param>
    /// <returns><see langword="true"/> if the profile was saved; otherwise <see langword="false"/>. Never throws, except on cancellation.</returns>
    Task<bool> TrySaveAsync(IReadOnlyList<ProfileValue> values, CancellationToken cancellationToken);
}
