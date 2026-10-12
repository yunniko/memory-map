using MemoryMap.Core;

namespace MemoryMap.Core.Tests;

internal static class TestData
{
    public const long Mb = 1024 * 1024;
    public const long Gb = 1024 * Mb;

    // The names a Windows snapshot carries (WindowsMemorySampler.Names).
    public static readonly PlatformNames WindowsNames = new("Windows & services", "WSL / Docker VMs", "Shared & other",
        "In use but owned by no single process: shared DLLs and memory, page tables and the like.");

    public static ProcessSample Proc(int pid, string name, long privateMb, ProcessKind kind = ProcessKind.App) =>
        new(pid, name + ".exe", kind, DateTimeOffset.UnixEpoch.AddSeconds(pid),
            privateMb * Mb, privateMb * Mb * 2, privateMb * Mb);

    public static SystemMemory Sys(
        long totalGb = 16,
        long availableMb = 4096,
        long standbyMb = 3000,
        long nonpagedMb = 500,
        long pagedResidentMb = 400,
        long cacheMb = 300,
        long driverMb = 20,
        long kernelCodeMb = 10,
        long modifiedMb = 100) =>
        new(
            Names: WindowsNames,
            TotalPhysical: totalGb * Gb,
            Available: availableMb * Mb,
            CommitTotal: 20 * Gb,
            CommitLimit: 40 * Gb,
            Kernel:
            [
                new("nonpaged-pool", "Nonpaged pool", nonpagedMb * Mb, "Kernel memory that can never be paged out; drivers allocate it.", true),
                new("paged-pool", "Paged pool (resident)", pagedResidentMb * Mb, "Kernel memory that may be paged out; this is the part in RAM now.", true),
                new("file-cache", "File cache (resident)", cacheMb * Mb, "File data the system cache holds in RAM."),
                new("driver-code", "Driver code", driverMb * Mb, "Driver code loaded in RAM."),
                new("kernel-code", "Kernel code", kernelCodeMb * Mb, "Windows kernel code loaded in RAM."),
            ],
            InUseNamed: [new("modified", "Modified (waiting to be written)", modifiedMb * Mb, "Changed pages waiting to be written.")],
            AvailableNamed: [new("standby", "Standby cache", standbyMb * Mb, "Cached data Windows hands to programs.")],
            Uptime: TimeSpan.FromHours(5));

    public static MemorySnapshot Snapshot(SystemMemory system, params ProcessSample[] processes) =>
        new(DateTimeOffset.UnixEpoch, system, processes);
}
