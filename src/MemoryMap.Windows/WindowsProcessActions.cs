using System.Diagnostics;
using System.Runtime.InteropServices;
using MemoryMap.Core;
using Microsoft.Win32.SafeHandles;

namespace MemoryMap.Windows;

/// <summary>
/// Ends processes and shows their files, on Windows. Before acting, each call opens the PID
/// and checks the process's start time against the sample: a PID freed and reused since the
/// sample belongs to a different process and is left alone. See D008.
/// </summary>
public sealed class WindowsProcessActions : IProcessActions
{
    private const int MaxPath = 32_767;

    public ActionResult End(ProcessSample process)
    {
        if (process.Pid == Environment.ProcessId)
        {
            return new(ActionOutcome.Refused, "Memory Map does not end itself; close its window instead.");
        }
        var (handle, refused) = Open(process, Native.ProcessTerminate | Native.ProcessQueryLimitedInformation, "end");
        if (refused is not null)
        {
            return refused;
        }
        using (handle)
        {
            if (Native.IsProcessCritical(handle!, out var critical) && critical)
            {
                return new(ActionOutcome.Refused,
                    $"{Label(process)} is a critical Windows process: ending it would crash the system, so Memory Map will not.");
            }
            if (!Native.TerminateProcess(handle!, 1))
            {
                return Failure(process, "end", Marshal.GetLastPInvokeError());
            }
            return new(ActionOutcome.Done, $"Ended {Label(process)}.");
        }
    }

    public ActionResult ShowInExplorer(ProcessSample process)
    {
        var (path, result) = ImagePath(process);
        if (path is null)
        {
            return result;
        }
        try
        {
            // Explorer's own syntax; the path is quoted because it may contain commas or spaces.
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new(ActionOutcome.Failed, $"Could not open Explorer at {path}: {e.Message}");
        }
        return new(ActionOutcome.Done, $"Showed {path}.");
    }

    /// <summary>The full path of the process's executable, or why it could not be read.</summary>
    internal static unsafe (string? Path, ActionResult Result) ImagePath(ProcessSample process)
    {
        var (handle, refused) = Open(process, Native.ProcessQueryLimitedInformation, "locate");
        if (refused is not null)
        {
            return (null, refused);
        }
        using (handle)
        {
            var buffer = new char[MaxPath];
            var size = (uint)buffer.Length;
            fixed (char* p = buffer)
            {
                if (!Native.QueryFullProcessImageName(handle!, 0, p, ref size))
                {
                    return (null, Failure(process, "locate", Marshal.GetLastPInvokeError()));
                }
            }
            var path = new string(buffer, 0, (int)size);
            return (path, new(ActionOutcome.Done, path));
        }
    }

    // Opens the PID and confirms it is still the sampled process and still running.
    private static (SafeProcessHandle? Handle, ActionResult? Refused) Open(ProcessSample process, uint access, string verb)
    {
        var handle = Native.OpenProcess(access, false, process.Pid);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            return (null, error == Native.ErrorInvalidParameter ? Exited(process) : Failure(process, verb, error));
        }
        var same = Native.GetProcessTimes(handle, out var created, out _, out _, out _)
            && DateTimeOffset.FromFileTime(created).UtcTicks == process.StartTime.UtcTicks;
        var running = Native.GetExitCodeProcess(handle, out var code) && code == Native.StillActive;
        if (!same || !running)
        {
            handle.Dispose();
            return (null, Exited(process));
        }
        return (handle, null);
    }

    private static ActionResult Exited(ProcessSample process) =>
        new(ActionOutcome.AlreadyExited, $"{Label(process)} has already exited.");

    private static ActionResult Failure(ProcessSample process, string verb, int error) => error == Native.ErrorAccessDenied
        ? new(ActionOutcome.AccessDenied,
            $"Windows denied access to {verb} {Label(process)}. It runs as another user, elevated, or protected; "
            + "Memory Map would need to run as administrator, and protected processes refuse even then.")
        : new(ActionOutcome.Failed, $"Could not {verb} {Label(process)}: {new System.ComponentModel.Win32Exception(error).Message}");

    private static string Label(ProcessSample process) => $"{process.Name} (PID {process.Pid})";
}
