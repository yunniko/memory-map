using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MemoryMap.Core;

namespace MemoryMap.Windows;

/// <summary>
/// Samples memory on Windows without WMI, cheaply enough to run every second:
/// one NtQuerySystemInformation call for all processes (no per-process handles, so
/// protected processes are included), GetPerformanceInfo for totals, and one PDH query
/// for the kernel and standby-list counters. Needs no elevation. 64-bit only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class WindowsMemorySampler : IMemorySampler
{
    private enum Counter
    {
        NonpagedPool,
        PagedPoolResident,
        SystemCacheResident,
        DriverCodeResident,
        KernelCodeResident,
        StandbyCore,
        StandbyNormal,
        StandbyReserve,
        ModifiedPages,
    }

    // \Memory counters, indexed by Counter; English names so the sampler works on any display language.
    private static readonly string[] CounterPaths =
    [
        @"\Memory\Pool Nonpaged Bytes",
        @"\Memory\Pool Paged Resident Bytes",
        @"\Memory\System Cache Resident Bytes",
        @"\Memory\System Driver Resident Bytes",
        @"\Memory\System Code Resident Bytes",
        @"\Memory\Standby Cache Core Bytes",
        @"\Memory\Standby Cache Normal Priority Bytes",
        @"\Memory\Standby Cache Reserve Bytes",
        @"\Memory\Modified Page List Bytes",
    ];

    /// <summary>Windows' names for its groups and the rest of in-use memory (D025).</summary>
    public static readonly PlatformNames Names = new("Windows & services", "WSL / Docker VMs", "Shared & other",
        "In use but owned by no single process: shared DLLs and memory, page tables and the like.");

    private readonly TimeProvider _clock;

    // Sample and Dispose share the PDH query and the buffer; a slow tick may overlap the next or a close.
    private readonly Lock _lock = new();
    private readonly nint _query;
    private readonly nint[] _counters = new nint[CounterPaths.Length];

    // Grows on demand; 512 KB holds about 500 processes with their threads.
    private byte[] _buffer = new byte[1 << 19];
    private bool _disposed;

    public WindowsMemorySampler(TimeProvider? clock = null)
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("The memory sampler reads 64-bit process structures and needs a 64-bit process.");
        }
        _clock = clock ?? TimeProvider.System;
        Check(Native.PdhOpenQuery(null, 0, out _query), "PdhOpenQuery");
        try
        {
            for (var i = 0; i < CounterPaths.Length; i++)
            {
                Check(Native.PdhAddEnglishCounter(_query, CounterPaths[i], 0, out _counters[i]), "PdhAddEnglishCounter", CounterPaths[i]);
            }
        }
        catch
        {
            // Dispose never runs on a half-built object: close the query here.
            _ = Native.PdhCloseQuery(_query);
            throw;
        }
    }

    public MemorySnapshot Sample()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var processes = ReadProcesses();
            var system = ReadSystem();
            return new MemorySnapshot(_clock.GetLocalNow(), system, processes);
        }
    }

    private SystemMemory ReadSystem()
    {
        if (!Native.GetPerformanceInfo(out var perf, (uint)sizeof(Native.PerformanceInformation)))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "GetPerformanceInfo failed.");
        }
        var page = (long)perf.PageSize;

        Check(Native.PdhCollectQueryData(_query), "PdhCollectQueryData");
        return new SystemMemory(
            Names: Names,
            TotalPhysical: (long)perf.PhysicalTotal * page,
            Available: (long)perf.PhysicalAvailable * page,
            CommitTotal: (long)perf.CommitTotal * page,
            CommitLimit: (long)perf.CommitLimit * page,
            Kernel:
            [
                new("nonpaged-pool", "Nonpaged pool", Read(Counter.NonpagedPool),
                    "Kernel memory that can never be paged out; drivers allocate it.", IsKernelAllocation: true),
                new("paged-pool", "Paged pool (resident)", Read(Counter.PagedPoolResident),
                    "Kernel memory that may be paged out; this is the part in RAM now.", IsKernelAllocation: true),
                new("file-cache", "File cache (resident)", Read(Counter.SystemCacheResident), "File data the system cache holds in RAM."),
                new("driver-code", "Driver code", Read(Counter.DriverCodeResident), "Driver code loaded in RAM."),
                new("kernel-code", "Kernel code", Read(Counter.KernelCodeResident), "Windows kernel code loaded in RAM."),
            ],
            InUseNamed:
            [
                new("modified", "Modified (waiting to be written)", Read(Counter.ModifiedPages),
                    "Changed pages waiting to be written to disk before they can be reused."),
            ],
            AvailableNamed:
            [
                new("standby", "Standby cache", Read(Counter.StandbyCore) + Read(Counter.StandbyNormal) + Read(Counter.StandbyReserve),
                    "Cached data Windows hands to programs as soon as they need it."),
            ],
            Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64));
    }

    private long Read(Counter counter)
    {
        var status = Native.PdhGetFormattedCounterValue(_counters[(int)counter], Native.PdhFmtLarge, out _, out var value);
        Check(status, "PdhGetFormattedCounterValue", CounterPaths[(int)counter]);
        if (value.CStatus is not Native.PdhCstatusValidData and not Native.PdhCstatusNewData)
        {
            throw new InvalidOperationException($"Counter {CounterPaths[(int)counter]} returned status 0x{value.CStatus:X8}.");
        }
        return value.LargeValue;
    }

    private List<ProcessSample> ReadProcesses()
    {
        // The process list can grow between the size probe and the read: retry with the size
        // Windows reports plus headroom.
        while (true)
        {
            uint needed;
            uint status;
            fixed (byte* p = _buffer)
            {
                status = Native.NtQuerySystemInformation(Native.SystemProcessInformation, p, (uint)_buffer.Length, out needed);
                if (status == Native.StatusSuccess)
                {
                    return Parse(p);
                }
            }
            if (status != Native.StatusInfoLengthMismatch)
            {
                throw new Win32Exception($"NtQuerySystemInformation failed with NTSTATUS 0x{status:X8}.");
            }
            _buffer = new byte[Math.Max(needed + (64 * 1024), _buffer.Length * 2)];
        }
    }

    private static List<ProcessSample> Parse(byte* start)
    {
        var list = new List<ProcessSample>(400);
        var entry = start;
        while (true)
        {
            var info = (Native.SystemProcessInfo*)entry;
            var pid = (int)info->UniqueProcessId;
            if (pid != 0) // pid 0 is the Idle process: no memory of its own.
            {
                var image = info->ImageNameBuffer == 0
                    ? "System"
                    : new string((char*)info->ImageNameBuffer, 0, info->ImageNameLength / 2);
                list.Add(new ProcessSample(
                    Pid: pid,
                    ImageName: image,
                    Kind: KindOf(image, (int)info->SessionId),
                    // System and Registry report no creation time: 1601-01-01 then.
                    StartTime: DateTimeOffset.FromFileTime(Math.Max(0, info->CreateTime)),
                    PrivateWorkingSet: info->WorkingSetPrivateSize,
                    WorkingSet: (long)info->WorkingSetSize,
                    PrivateCommit: (long)info->PrivatePageCount));
            }
            if (info->NextEntryOffset == 0)
            {
                return list;
            }
            entry += info->NextEntryOffset;
        }
    }

    /// <summary>vmmem and vmmemWSL hold a Hyper-V VM's RAM; session 0 holds services and the system.</summary>
    internal static ProcessKind KindOf(string imageName, int sessionId) =>
        imageName.StartsWith("vmmem", StringComparison.OrdinalIgnoreCase) ? ProcessKind.VirtualMachine
        : sessionId == 0 ? ProcessKind.Service
        : ProcessKind.App;

    private static void Check(uint status, string call, string? subject = null)
    {
        if (status != 0)
        {
            throw new Win32Exception(subject is null
                ? $"{call} failed with status 0x{status:X8}."
                : $"{call}({subject}) failed with status 0x{status:X8}.");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _ = Native.PdhCloseQuery(_query);
        }
    }
}
