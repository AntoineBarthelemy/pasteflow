using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Records what the learner tells, and answers its questions as the test decided.
/// </summary>
internal sealed class FakeLearningPrompts : IProfileLearningPrompts
{
    public List<ProfileValue> Added { get; } = new();

    public List<Func<Task>> Undos { get; } = new();

    public List<ProfileValue> Asked { get; } = new();

    public bool Answer { get; set; } = true;

    public void ShowAdded(ProfileValue value, Func<Task> undo)
    {
        Added.Add(value);
        Undos.Add(undo);
    }

    public Task<bool> AskToAddSensitiveAsync(ProfileValue value, CancellationToken cancellationToken)
    {
        Asked.Add(value);
        return Task.FromResult(Answer);
    }
}
