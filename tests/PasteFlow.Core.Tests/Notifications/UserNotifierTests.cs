using PasteFlow.Core.Forms;
using PasteFlow.Core.Notifications;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Notifications;

public sealed class UserNotifierTests
{
    [Fact]
    public void NotifyFill_ByDefault_ShowsTheReportAsText()
    {
        var notifier = new FakeUserNotifier();
        var report = new FillReport
        {
            FilledCount = 0,
            UncertainCount = 0,
            AlreadyFilledCount = 0,
            FailedCount = 0,
            Sources = Array.Empty<SourceSummary>(),
            Elapsed = TimeSpan.FromMilliseconds(80),
        };

        ((IUserNotifier)notifier).NotifyFill(report);

        Assert.Equal(report.Describe(), Assert.Single(notifier.Messages));
    }
}
