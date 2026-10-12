using System.Runtime.InteropServices;
using PasteFlow.Core.Security;
using Windows.Security.Credentials.UI;

namespace PasteFlow.App.Security;

/// <summary>
/// Checks the user with Windows Hello: face, fingerprint or the PIN of Windows, whatever the computer has.
/// </summary>
/// <remarks>
/// The PIN is part of Windows Hello and Windows 11 sets it up from the start, so nearly every computer has it.
/// Without Windows Hello at all, the check says it is unavailable and the sensitive values stay closed.
/// </remarks>
internal sealed class WindowsHelloPresenceCheck : IUserPresenceCheck
{
    /// <inheritdoc/>
    public async Task<PresenceResult> VerifyAsync(string reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        try
        {
            var availability = await UserConsentVerifier.CheckAvailabilityAsync().AsTask(cancellationToken);
            if (availability != UserConsentVerifierAvailability.Available)
            {
                return PresenceResult.Unavailable;
            }

            var result = await UserConsentVerifier.RequestVerificationAsync(reason).AsTask(cancellationToken);
            return result == UserConsentVerificationResult.Verified ? PresenceResult.Verified : PresenceResult.Refused;
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            return PresenceResult.Unavailable;
        }
    }
}
