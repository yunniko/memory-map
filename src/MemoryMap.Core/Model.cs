namespace MemoryMap.Core;

/// <summary>What a process is, as the OS-specific sampler judges it. Core groups by this alone.</summary>
public enum ProcessKind
{
    /// <summary>A program of a signed-in user (Windows: session 1 and up; Linux: a regular user account).</summary>
    App,

    /// <summary>A service or system process (Windows: session 0; Linux: root and system accounts).</summary>
    Service,

    /// <summary>A virtual machine or container (Windows: vmmem / vmmemWSL; Linux: qemu and the like, or a container's cgroup).</summary>
    VirtualMachine,
}

/// <summary>
/// The words that differ by OS (D025): the names of the system and VM groups, and of what is left
/// of in-use memory once processes, kernel and the named parts are counted.
/// </summary>
public sealed record PlatformNames(string SystemGroup, string VirtualMachineGroup, string SharedRestName, string SharedRestNote);

/// <summary>One process as the sampler saw it. All sizes are bytes.</summary>
/// <param name="ImageName">The executable name as the OS reports it, e.g. "chrome.exe" or "firefox".</param>
/// <param name="StartTime">With <paramref name="Pid"/>, identifies the process even after its pid is reused.</param>
/// <param name="PrivateWorkingSet">Physical RAM only this process uses: the map's measure (Windows: Task
/// Manager's "Memory" column; Linux: resident anonymous memory, D026).</param>
/// <param name="WorkingSet">Physical RAM the process touches, including pages shared with others.</param>
/// <param name="PrivateCommit">Private memory the process has committed, resident or paged out.</param>
public sealed record ProcessSample(
    int Pid,
    string ImageName,
    ProcessKind Kind,
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

/// <summary>One named system-wide amount of RAM, such as the nonpaged pool or the slab. Bytes.</summary>
/// <param name="Id">Stable across samples; part of the map node's key.</param>
/// <param name="Note">One sentence for the tooltip saying what the memory is.</param>
/// <param name="IsKernelAllocation">Counts toward <see cref="SystemMemory.KernelAllocations"/>.</param>
public sealed record MemoryFigure(string Id, string Name, long Bytes, string Note, bool IsKernelAllocation = false);

/// <summary>
/// System-wide memory figures. All sizes are bytes. The figure lists are the OS's own
/// breakdown; Core treats them alike on every OS (D025).
/// </summary>
/// <param name="Available">What programs can get without paging anything out (Windows: free plus
/// standby; Linux: MemAvailable).</param>
/// <param name="Names">The OS's names for its groups and remainder.</param>
/// <param name="Kernel">Kernel memory in RAM, owned by no process.</param>
/// <param name="InUseNamed">Named parts of in-use memory that no process or kernel figure owns,
/// such as Windows' modified list or Linux's shared memory. Taken from what is left, in order.</param>
/// <param name="AvailableNamed">Named parts of <paramref name="Available"/> such as the standby
/// cache; the rest of it is free.</param>
public sealed record SystemMemory(
    PlatformNames Names,
    long TotalPhysical,
    long Available,
    long CommitTotal,
    long CommitLimit,
    IReadOnlyList<MemoryFigure> Kernel,
    IReadOnlyList<MemoryFigure> InUseNamed,
    IReadOnlyList<MemoryFigure> AvailableNamed,
    TimeSpan Uptime)
{
    public long Used => TotalPhysical - Available;

    /// <summary>The kernel's own allocations in RAM, the history's kernel line (Windows: nonpaged plus
    /// resident paged pool; Linux: every kernel figure).</summary>
    public long KernelAllocations => Kernel.Where(f => f.IsKernelAllocation).Sum(f => f.Bytes);

    /// <summary>Share of RAM available, 0-100; 100 when the total is unknown, so nothing looks low.</summary>
    public double AvailablePercent => TotalPhysical > 0 ? 100.0 * Available / TotalPhysical : 100;

    public double UsedPercent => 100 - AvailablePercent;
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
