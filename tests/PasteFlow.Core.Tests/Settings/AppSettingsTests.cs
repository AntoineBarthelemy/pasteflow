using PasteFlow.Core.Matching;
using PasteFlow.Core.Settings;

namespace PasteFlow.Core.Tests.Settings;

public sealed class AppSettingsTests
{
    [Fact]
    public void Default_IsBalancedLearnsAndDoesNotStartWithWindows()
    {
        Assert.Equal(Prudence.Balanced, AppSettings.Default.Prudence);
        Assert.True(AppSettings.Default.LearnFromFills);
        Assert.False(AppSettings.Default.StartWithWindows);
        Assert.True(AppSettings.Default.ReadImages);
    }
}
