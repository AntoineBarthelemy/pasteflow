using PasteFlow.Core.Security;

namespace PasteFlow.App.Tests.Fakes;

/// <summary>
/// Answers the checks of presence as the test decided, and counts them.
/// </summary>
internal sealed class FakePresenceCheck : IUserPresenceCheck
{
    public PresenceResult Answer { get; set; } = PresenceResult.Verified;

    public int Count { get; private set; }

    public Task<PresenceResult> VerifyAsync(string reason, CancellationToken cancellationToken)
    {
        Count++;
        return Task.FromResult(Answer);
    }
}
