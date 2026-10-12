namespace PasteFlow.Core.Profile;

/// <summary>
/// Tells the user what PasteFlow learned about them, and asks before keeping a sensitive value.
/// </summary>
public interface IProfileLearningPrompts
{
    /// <summary>
    /// Tells, without stopping the user, that a value was added to the profile, with a way to take it back.
    /// </summary>
    /// <param name="value">The value added.</param>
    /// <param name="undo">Takes the value back out of the profile.</param>
    void ShowAdded(ProfileValue value, Func<Task> undo);

    /// <summary>
    /// Asks the user whether to keep a sensitive value in the profile.
    /// </summary>
    /// <param name="value">The value, which the question shows hidden, by its last characters only.</param>
    /// <param name="cancellationToken">A token to cancel the question.</param>
    /// <returns><see langword="true"/> if the user agrees; otherwise <see langword="false"/>.</returns>
    Task<bool> AskToAddSensitiveAsync(ProfileValue value, CancellationToken cancellationToken);
}
