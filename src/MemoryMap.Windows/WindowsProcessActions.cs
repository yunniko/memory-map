using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using MemoryMap.Core;
using Microsoft.Win32.SafeHandles;

namespace MemoryMap.Windows;

/// <summary>
/// Ends processes and shows their files, on Windows. Before acting, each call opens the PID
/// and checks the process's start time against the sample: a PID freed and reused since the
/// sample belongs to a different process and is left alone. The action then runs on that same
/// handle, so the PID cannot change hands in between. See D008.
/// </summary>
public sealed class WindowsProcessActions : IProcessActions
{
    private const int ShortPath = 1024;
    private const int MaxPath = 32_767;

    public ActionResult End(ProcessSample process)
    {
        if (process.Pid == Environment.ProcessId)
        {
            return ActionResult.Self();
        }
        if (!TryOpen(process, Native.ProcessTerminate | Native.ProcessQueryLimitedInformation, "end", out var handle, out var failure))
        {
            return failure;
        }
        using (handle)
        {
            // Fail closed: if Windows cannot say whether the process is critical, do not end it.
            if (!Native.IsProcessCritical(handle, out var critical))
            {
                return Failure(process, "end", Marshal.GetLastPInvokeError());
            }
            if (critical)
            {
                return ActionResult.Critical(process);
            }
            return Native.TerminateProcess(handle, 1)
                ? ActionResult.Ended(process)
                : Failure(process, "end", Marshal.GetLastPInvokeError());
        }
    }

    public ActionResult ShowInExplorer(ProcessSample process)
    {
        if (!TryImagePath(process, out var path, out var failure))
        {
            return failure;
        }
        try
        {
            // Explorer's own syntax; the path is quoted because it may contain commas or spaces.
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return ActionResult.Failed(process, "show", $"Explorer did not start ({e.Message}).");
        }
        return ActionResult.Shown(path);
    }

    /// <summary>The full path of the process's executable, or why it could not be read.</summary>
    internal static bool TryImagePath(ProcessSample process, [NotNullWhen(true)] out string? path, [NotNullWhen(false)] out ActionResult? failure)
    {
        path = null;
        if (!TryOpen(process, Native.ProcessQueryLimitedInformation, "locate", out var handle, out failure))
        {
            return false;
        }
        using (handle)
        {
            // Most paths fit the short buffer; only a long path pays for the full-size one.
            var error = Query(handle, stackalloc char[ShortPath], out path);
            if (error == Native.ErrorInsufficientBuffer)
            {
                error = Query(handle, new char[MaxPath], out path);
            }
            failure = error == 0 ? null : Failure(process, "locate", error);
            return error == 0;
        }
    }

    private static unsafe int Query(SafeProcessHandle handle, Span<char> buffer, out string? path)
    {
        var size = (uint)buffer.Length;
        fixed (char* p = buffer)
        {
            if (!Native.QueryFullProcessImageName(handle, 0, p, ref size))
            {
                path = null;
                return Marshal.GetLastPInvokeError();
            }
        }
        path = new string(buffer[..(int)size]);
        return 0;
    }

    // Opens the PID and confirms it is still the sampled process and still running.
    private static bool TryOpen(ProcessSample process, uint access, string verb,
        [NotNullWhen(true)] out SafeProcessHandle? handle, [NotNullWhen(false)] out ActionResult? failure)
    {
        handle = Native.OpenProcess(access, false, process.Pid);
        failure = null;
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastPInvokeError();
            failure = error == Native.ErrorInvalidParameter ? ActionResult.Exited(process) : Failure(process, verb, error);
        }
        else if (!Native.GetProcessTimes(handle, out var created, out _, out _, out _)
            || !Native.GetExitCodeProcess(handle, out var code))
        {
            failure = Failure(process, verb, Marshal.GetLastPInvokeError());
        }
        else if (DateTimeOffset.FromFileTime(created).UtcTicks != process.StartTime.UtcTicks || code != Native.StillActive)
        {
            failure = ActionResult.Exited(process);
        }
        if (failure is null)
        {
            return true;
        }
        handle.Dispose();
        handle = null;
        return false;
    }

    private static ActionResult Failure(ProcessSample process, string verb, int error) => error == Native.ErrorAccessDenied
        ? ActionResult.Denied(process, verb)
        : ActionResult.Failed(process, verb, new System.ComponentModel.Win32Exception(error).Message);
}
