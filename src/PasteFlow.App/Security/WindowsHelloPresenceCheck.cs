using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PasteFlow.Core.Security;
using Windows.Security.Credentials.UI;

namespace PasteFlow.App.Security;

/// <summary>
/// Checks the user with Windows Hello: face, fingerprint or the PIN of Windows, whatever the computer has.
/// </summary>
/// <remarks>
/// <para>A desktop application must attach the Windows Hello dialog to one of its own windows (the method without a
/// window is meant for Store applications only): a tiny invisible window is opened in the middle of the screen for
/// the time of the check, so that the dialog shows in front of the browser.</para>
/// <para>The PIN is part of Windows Hello and Windows 11 sets it up from the start, so nearly every computer has it.
/// Without Windows Hello at all, the check says it is unavailable and the sensitive values stay closed.</para>
/// </remarks>
internal sealed class WindowsHelloPresenceCheck : IUserPresenceCheck
{
    private readonly Dispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsHelloPresenceCheck"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher of the user interface thread, where the window of the dialog is opened.</param>
    public WindowsHelloPresenceCheck(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
    }

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

            var ask = await _dispatcher.InvokeAsync(() => AskAsync(reason, cancellationToken), DispatcherPriority.Normal, cancellationToken);
            return ResultOf(await ask);
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            return PresenceResult.Unavailable;
        }
    }

    /// <summary>
    /// Tells what an answer of Windows Hello means for PasteFlow.
    /// </summary>
    /// <param name="result">The answer of Windows Hello.</param>
    /// <returns>Verified, refused by the user, or unavailable on this computer.</returns>
    internal static PresenceResult ResultOf(UserConsentVerificationResult result)
    {
        return result switch
        {
            UserConsentVerificationResult.Verified => PresenceResult.Verified,
            UserConsentVerificationResult.Canceled or UserConsentVerificationResult.RetriesExhausted => PresenceResult.Refused,
            _ => PresenceResult.Unavailable,
        };
    }

    private static async Task<UserConsentVerificationResult> AskAsync(string reason, CancellationToken cancellationToken)
    {
        var owner = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Width = 1,
            Height = 1,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Title = "PasteFlow",
        };
        owner.Show();
        _ = owner.Activate();
        try
        {
            var handle = new WindowInteropHelper(owner).Handle;
            return await UserConsentVerifierInterop.RequestVerificationForWindowAsync(handle, reason).AsTask(cancellationToken);
        }
        finally
        {
            owner.Close();
        }
    }
}
