using PasteFlow.Core.Security;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Answers the checks of presence as the test decided, and counts them.
/// </summary>
internal sealed class FakePresenceCheck : IUserPresenceCheck
{
    public PresenceResult Answer { get; set; } = PresenceResult.Verified;

    public int Count { get; private set; }

    public string LastReason { get; private set; } = string.Empty;

    public Task<PresenceResult> VerifyAsync(string reason, CancellationToken cancellationToken)
    {
        Count++;
        LastReason = reason;
        return Task.FromResult(Answer);
    }
}
