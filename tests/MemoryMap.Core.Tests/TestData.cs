using MemoryMap.Core;

namespace MemoryMap.Core.Tests;

internal static class TestData
{
    public const long Mb = 1024 * 1024;
    public const long Gb = 1024 * Mb;

    public static ProcessSample Proc(int pid, string name, long privateMb, ProcessKind kind = ProcessKind.App) =>
        new(pid, name + ".exe", kind, kind == ProcessKind.App ? 1 : 0, DateTimeOffset.UnixEpoch.AddSeconds(pid),
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
            TotalPhysical: totalGb * Gb,
            Available: availableMb * Mb,
            Standby: standbyMb * Mb,
            CommitTotal: 20 * Gb,
            CommitLimit: 40 * Gb,
            NonpagedPool: nonpagedMb * Mb,
            PagedPoolResident: pagedResidentMb * Mb,
            PagedPoolTotal: pagedResidentMb * Mb * 2,
            SystemCacheResident: cacheMb * Mb,
            DriverCodeResident: driverMb * Mb,
            KernelCodeResident: kernelCodeMb * Mb,
            ModifiedPages: modifiedMb * Mb,
            Uptime: TimeSpan.FromHours(5));

    public static MemorySnapshot Snapshot(SystemMemory system, params ProcessSample[] processes) =>
        new(DateTimeOffset.UnixEpoch, system, processes);
}
