using PasteFlow.App.Security;
using PasteFlow.Core.Security;
using Windows.Security.Credentials.UI;

namespace PasteFlow.App.Tests.Security;

public sealed class WindowsHelloPresenceCheckTests
{
    [Theory]
    [InlineData(UserConsentVerificationResult.Verified, PresenceResult.Verified)]
    [InlineData(UserConsentVerificationResult.Canceled, PresenceResult.Refused)]
    [InlineData(UserConsentVerificationResult.RetriesExhausted, PresenceResult.Refused)]
    [InlineData(UserConsentVerificationResult.DeviceNotPresent, PresenceResult.Unavailable)]
    [InlineData(UserConsentVerificationResult.NotConfiguredForUser, PresenceResult.Unavailable)]
    [InlineData(UserConsentVerificationResult.DisabledByPolicy, PresenceResult.Unavailable)]
    [InlineData(UserConsentVerificationResult.DeviceBusy, PresenceResult.Unavailable)]
    public void ResultOf_TellsWhatTheAnswerMeans(UserConsentVerificationResult answer, PresenceResult expected)
    {
        Assert.Equal(expected, WindowsHelloPresenceCheck.ResultOf(answer));
    }
}
