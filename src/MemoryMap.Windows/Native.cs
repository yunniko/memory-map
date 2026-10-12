using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MemoryMap.Windows;

#pragma warning disable IDE1006 // Interop structs keep the Windows SDK's field names.

internal static partial class Native
{
    public const int SystemProcessInformation = 5;
    public const uint StatusSuccess = 0;
    public const uint StatusInfoLengthMismatch = 0xC0000004;

    public const uint PdhFmtLarge = 0x00000400;
    public const uint PdhCstatusValidData = 0;
    public const uint PdhCstatusNewData = 1;

    [LibraryImport("ntdll.dll")]
    public static unsafe partial uint NtQuerySystemInformation(int infoClass, void* buffer, uint length, out uint returnLength);

    [LibraryImport("kernel32.dll", EntryPoint = "K32GetPerformanceInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPerformanceInfo(out PerformanceInformation info, uint size);

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhOpenQuery(string? dataSource, nint userData, out nint query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhAddEnglishCounter(nint query, string fullCounterPath, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCollectQueryData(nint query);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out PdhFmtCounterValue value);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCloseQuery(nint query);

    public const uint ProcessTerminate = 0x0001;
    public const uint ProcessQueryLimitedInformation = 0x1000;
    public const uint Synchronize = 0x00100000;
    public const int ErrorAccessDenied = 5;
    public const int ErrorInvalidParameter = 87;
    public const uint StillActive = 259;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int pid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsProcessCritical(SafeProcessHandle process, [MarshalAs(UnmanagedType.Bool)] out bool critical);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TerminateProcess(SafeProcessHandle process, uint exitCode);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, char* buffer, ref uint size);

    [StructLayout(LayoutKind.Sequential)]
    public struct PerformanceInformation
    {
        public uint cb;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PdhFmtCounterValue
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public long LargeValue;
    }

    /// <summary>
    /// The 64-bit layout of SYSTEM_PROCESS_INFORMATION (as documented by the Process Hacker
    /// "phnt" headers; stable since Windows 8). Only the fields the sampler reads are declared.
    /// The process's thread array follows the struct; entries are chained by NextEntryOffset.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct SystemProcessInfo
    {
        [FieldOffset(0)] public uint NextEntryOffset;
        [FieldOffset(8)] public long WorkingSetPrivateSize;
        [FieldOffset(32)] public long CreateTime;
        [FieldOffset(56)] public ushort ImageNameLength;
        [FieldOffset(64)] public nint ImageNameBuffer;
        [FieldOffset(80)] public nint UniqueProcessId;
        [FieldOffset(100)] public uint SessionId;
        [FieldOffset(144)] public nuint WorkingSetSize;
        [FieldOffset(200)] public nuint PrivatePageCount;
    }
}
