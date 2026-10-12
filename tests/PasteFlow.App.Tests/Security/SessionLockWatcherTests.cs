using Microsoft.Win32;
using PasteFlow.App.Security;

namespace PasteFlow.App.Tests.Security;

public sealed class SessionLockWatcherTests
{
    [Theory]
    [InlineData(SessionSwitchReason.SessionLock, true)]
    [InlineData(SessionSwitchReason.SessionLogoff, true)]
    [InlineData(SessionSwitchReason.ConsoleDisconnect, true)]
    [InlineData(SessionSwitchReason.RemoteDisconnect, true)]
    [InlineData(SessionSwitchReason.SessionUnlock, false)]
    [InlineData(SessionSwitchReason.SessionLogon, false)]
    public void ClosesTheGate_WhenTheUserLeavesTheComputer(SessionSwitchReason reason, bool closes)
    {
        Assert.Equal(closes, SessionLockWatcher.ClosesTheGate(reason));
    }
}
