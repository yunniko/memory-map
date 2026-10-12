using System.Runtime.InteropServices;

namespace MemoryMap.Linux;

/// <summary>The C library calls the Linux code needs, in one place.</summary>
internal static partial class Libc
{
    [LibraryImport("libc", EntryPoint = "syscall", SetLastError = true)]
    public static partial long Syscall(long number, long a, long b, long c, long d);

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    public static partial int Kill(int pid, int signal);

    [LibraryImport("libc", EntryPoint = "getuid")]
    public static partial uint GetUid();

    /// <summary>The path with every symbolic link and "..", in the folders as well as the file,
    /// resolved, as /proc reports a process's executable; null if it does not exist.</summary>
    public static string? RealPath(string path)
    {
        var resolved = RealPath(path, 0);
        if (resolved == 0)
        {
            return null;
        }
        try
        {
            return Marshal.PtrToStringUTF8(resolved);
        }
        finally
        {
            Free(resolved);
        }
    }

    [LibraryImport("libc", EntryPoint = "realpath", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint RealPath(string path, nint resolved);

    [LibraryImport("libc", EntryPoint = "free")]
    private static partial void Free(nint pointer);
}
