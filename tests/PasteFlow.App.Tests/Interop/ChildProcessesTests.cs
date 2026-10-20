using PasteFlow.App.Interop;

namespace PasteFlow.App.Tests.Interop;

public sealed class ChildProcessesTests
{
    [Fact]
    public void CloseWithThisProcess_IsAcceptedByWindowsAndCanBeAskedTwice()
    {
        Assert.True(ChildProcesses.CloseWithThisProcess());
        Assert.True(ChildProcesses.CloseWithThisProcess());
    }
}
