using System.Runtime.InteropServices;

namespace PasteFlow.App.Interop;

/// <summary>
/// The limits of a group of processes (JOBOBJECT_EXTENDED_LIMIT_INFORMATION): only <see cref="LimitFlags"/> is used.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JobLimits
{
    /// <summary>
    /// Unused: time limit of each process.
    /// </summary>
    public long PerProcessUserTimeLimit;

    /// <summary>
    /// Unused: time limit of the group.
    /// </summary>
    public long PerJobUserTimeLimit;

    /// <summary>
    /// The limits that apply, such as 0x2000 (JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE).
    /// </summary>
    public uint LimitFlags;

    /// <summary>
    /// Unused: smallest memory kept.
    /// </summary>
    public nuint MinimumWorkingSetSize;

    /// <summary>
    /// Unused: largest memory kept.
    /// </summary>
    public nuint MaximumWorkingSetSize;

    /// <summary>
    /// Unused: largest number of processes.
    /// </summary>
    public uint ActiveProcessLimit;

    /// <summary>
    /// Unused: processors allowed.
    /// </summary>
    public nuint Affinity;

    /// <summary>
    /// Unused: priority.
    /// </summary>
    public uint PriorityClass;

    /// <summary>
    /// Unused: scheduling class.
    /// </summary>
    public uint SchedulingClass;

    /// <summary>
    /// Unused: read operations.
    /// </summary>
    public ulong ReadOperationCount;

    /// <summary>
    /// Unused: write operations.
    /// </summary>
    public ulong WriteOperationCount;

    /// <summary>
    /// Unused: other operations.
    /// </summary>
    public ulong OtherOperationCount;

    /// <summary>
    /// Unused: bytes read.
    /// </summary>
    public ulong ReadTransferCount;

    /// <summary>
    /// Unused: bytes written.
    /// </summary>
    public ulong WriteTransferCount;

    /// <summary>
    /// Unused: other bytes.
    /// </summary>
    public ulong OtherTransferCount;

    /// <summary>
    /// Unused: memory limit of each process.
    /// </summary>
    public nuint ProcessMemoryLimit;

    /// <summary>
    /// Unused: memory limit of the group.
    /// </summary>
    public nuint JobMemoryLimit;

    /// <summary>
    /// Unused: highest memory of a process.
    /// </summary>
    public nuint PeakProcessMemoryUsed;

    /// <summary>
    /// Unused: highest memory of the group.
    /// </summary>
    public nuint PeakJobMemoryUsed;
}
