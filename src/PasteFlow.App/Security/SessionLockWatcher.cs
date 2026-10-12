using Microsoft.Win32;
using PasteFlow.Core.Security;

namespace PasteFlow.App.Security;

/// <summary>
/// Closes the sensitive gate as soon as the computer is locked, goes to sleep or the user signs out.
/// </summary>
internal sealed class SessionLockWatcher : IDisposable
{
    private readonly SensitiveGate _gate;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionLockWatcher"/> class and starts watching.
    /// </summary>
    /// <param name="gate">The gate to close.</param>
    public SessionLockWatcher(SensitiveGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);

        _gate = gate;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    /// <summary>
    /// Determines whether a change of the session must close the gate.
    /// </summary>
    /// <param name="reason">The change.</param>
    /// <returns><see langword="true"/> when the user leaves the computer; otherwise <see langword="false"/>.</returns>
    internal static bool ClosesTheGate(SessionSwitchReason reason)
    {
        return reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff
            or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (ClosesTheGate(e.Reason))
        {
            _gate.Close();
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _gate.Close();
        }
    }
}
