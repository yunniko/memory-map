namespace MemoryMap.Core;

/// <summary>What a process is, as the OS-specific sampler judges it. Core groups by this alone.</summary>
public enum ProcessKind
{
    /// <summary>A program in the user's interactive session.</summary>
    App,

    /// <summary>A service or system process (on Windows: session 0).</summary>
    Service,

    /// <summary>The host process of a virtual machine (on Windows: vmmem / vmmemWSL).</summary>
    VirtualMachine,
}

/// <summary>One process as the sampler saw it. All sizes are bytes.</summary>
/// <param name="ImageName">The executable name as the OS reports it, e.g. "chrome.exe".</param>
/// <param name="StartTime">With <paramref name="Pid"/>, identifies the process even after its pid is reused.</param>
/// <param name="PrivateWorkingSet">Physical RAM only this process uses (Task Manager's "Memory" column).</param>
/// <param name="WorkingSet">Physical RAM the process touches, including pages shared with others.</param>
/// <param name="PrivateCommit">Private memory the process has committed, resident or paged out.</param>
public sealed record ProcessSample(
    int Pid,
    string ImageName,
    ProcessKind Kind,
    int SessionId,
    DateTimeOffset StartTime,
    long PrivateWorkingSet,
    long WorkingSet,
    long PrivateCommit)
{
    /// <summary>The image name without a trailing ".exe".</summary>
    public string Name =>
        ImageName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? ImageName[..^4] : ImageName;

    /// <summary>"chrome (PID 42)": how messages and menus name one process.</summary>
    public string Label => $"{Name} (PID {Pid})";
}

/// <summary>System-wide memory figures. All sizes are bytes.</summary>
/// <param name="Available">Free plus standby memory: what programs can get without paging anything out.</param>
/// <param name="Standby">Cached pages Windows hands to programs on demand; part of <see cref="Available"/>.</param>
/// <param name="PagedPoolResident">The part of the paged pool that is in RAM right now.</param>
/// <param name="PagedPoolTotal">The whole paged pool, including what is paged out.</param>
/// <param name="ModifiedPages">Changed pages waiting to be written to disk: in use, owned by no working set.</param>
public sealed record SystemMemory(
    long TotalPhysical,
    long Available,
    long Standby,
    long CommitTotal,
    long CommitLimit,
    long NonpagedPool,
    long PagedPoolResident,
    long PagedPoolTotal,
    long SystemCacheResident,
    long DriverCodeResident,
    long KernelCodeResident,
    long ModifiedPages,
    TimeSpan Uptime)
{
    public long Used => TotalPhysical - Available;
}

/// <summary>
/// One sample of the machine. The figures are read one after another within a few
/// milliseconds, not atomically, so they can disagree slightly; <see cref="Taken"/> is
/// stamped once all reads are done.
/// </summary>
public sealed record MemorySnapshot(DateTimeOffset Taken, SystemMemory System, IReadOnlyList<ProcessSample> Processes);

/// <summary>Takes snapshots of the machine's memory. One implementation per OS.</summary>
public interface IMemorySampler : IDisposable
{
    MemorySnapshot Sample();
}
