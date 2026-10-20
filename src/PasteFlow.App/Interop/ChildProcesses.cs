using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PasteFlow.App.Interop;

/// <summary>
/// Makes the programs PasteFlow starts close with it, even when it is stopped abruptly: the local service of Bitwarden
/// must never stay open with the vault unlocked once PasteFlow is gone.
/// </summary>
/// <remarks>
/// PasteFlow joins a group of processes that Windows ends as soon as PasteFlow ends; the programs it starts afterwards
/// join the group too, from their very first instant.
/// </remarks>
internal static class ChildProcesses
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;

    private static readonly Lock Gate = new();

    // Kept open until PasteFlow ends: Windows closes it then, which ends the group
    private static IntPtr _job;

    /// <summary>
    /// Makes the programs started from now on close with PasteFlow.
    /// </summary>
    /// <returns><see langword="true"/> if Windows accepted; otherwise <see langword="false"/>.</returns>
    public static bool CloseWithThisProcess()
    {
        lock (Gate)
        {
            if (_job != IntPtr.Zero)
            {
                return true;
            }

            var job = NativeMethods.CreateJobObject(IntPtr.Zero, IntPtr.Zero);
            if (job == IntPtr.Zero)
            {
                return false;
            }

            var limits = new JobLimits { LimitFlags = KillOnJobClose };
            using var current = Process.GetCurrentProcess();
            if (!NativeMethods.SetInformationJobObject(job, ExtendedLimitInformation, ref limits, (uint)Marshal.SizeOf<JobLimits>())
                || !NativeMethods.AssignProcessToJobObject(job, current.Handle))
            {
                return false;
            }

            _job = job;
            return true;
        }
    }
}
