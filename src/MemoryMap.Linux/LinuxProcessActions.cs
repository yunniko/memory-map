using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using MemoryMap.Core;
using Microsoft.Win32.SafeHandles;

namespace MemoryMap.Linux;

/// <summary>
/// Ends processes and shows their files, on Linux, for the user's own processes only: others'
/// would need root, which the app never asks for (G-003 criterion 2). Each call pins the PID
/// with a pidfd, checks the process's start time against the sample, and acts through that
/// pidfd, so a PID reused since the sample is left alone (D008, D029).
/// </summary>
public sealed class LinuxProcessActions(IProcFs? proc = null, Func<string, string?>? showFile = null) : IProcessActions
{
    private const int InitPid = 1;
    private const int SigKill = 9;
    private const int Esrch = 3;
    private const int Eperm = 1;
    private const int Enosys = 38;
    private const long SysPidfdSendSignal = 424; // the same number on every architecture
    private const long SysPidfdOpen = 434;

    private readonly IProcFs _proc = proc ?? new ProcFs();
    private readonly Func<string, string?> _showFile = showFile ?? Desktop.ShowFile;
    public ActionResult End(ProcessSample process)
    {
        if (process.Pid == Environment.ProcessId)
        {
            return ActionResult.Self();
        }
        if (process.Pid == InitPid)
        {
            return ActionResult.Critical(process); // the kernel panics when init dies
        }
        if (!TryPin(process, "end", out var pidfd, out var failure))
        {
            return failure;
        }
        using (pidfd)
        {
            // SIGKILL, as Windows' TerminateProcess: the confirmation says it closes without saving.
            return pidfd.Signal(SigKill) switch
            {
                0 => ActionResult.Ended(process),
                Esrch => ActionResult.Exited(process),
                Eperm => ActionResult.NotYours(process, "end"),
                var error => ActionResult.Failed(process, "end", Marshal.GetPInvokeErrorMessage(error)),
            };
        }
    }

    public ActionResult ShowFileLocation(ProcessSample process)
    {
        if (!TryPin(process, "locate", out var pidfd, out var failure))
        {
            return failure;
        }
        string? link;
        using (pidfd)
        {
            link = _proc.ReadLink($"{process.Pid}/exe");
            // Read through the PID: it must still be the pinned process afterwards.
            if (pidfd.Signal(0) == Esrch)
            {
                return ActionResult.Exited(process);
            }
        }
        if (link is not { Length: > 0 })
        {
            return ActionResult.Failed(process, "locate", "its program file cannot be read (a kernel or sandboxed process).");
        }
        var path = ProcText.ExePath(link);
        return _showFile(path) is { } error ? ActionResult.Failed(process, "show", error) : ActionResult.Shown(path);
    }

    // Pins the PID and confirms it is still the sampled process, and the user's own.
    private bool TryPin(ProcessSample process, string verb, out PidFd pidfd, [NotNullWhen(false)] out ActionResult? failure)
    {
        pidfd = PidFd.Open(process.Pid);
        failure = null;
        if (pidfd.Error == Esrch)
        {
            failure = ActionResult.Exited(process);
        }
        else if (pidfd.Error is not (0 or Enosys or Eperm))
        {
            failure = ActionResult.Failed(process, verb, Marshal.GetPInvokeErrorMessage(pidfd.Error));
        }
        else if (_proc.ReadText($"{process.Pid}/stat") is not { } statText || ProcText.Stat(statText) is not { } stat
            || ProcClock.StartTime(ProcClock.BootTime(_proc), stat.StartTicks).UtcTicks != process.StartTime.UtcTicks
            || _proc.ReadText($"{process.Pid}/status") is not { } status)
        {
            failure = ActionResult.Exited(process);
        }
        else if (ProcText.Uid(ProcText.Fields(status)) is var uid && (uid < 0 || (uint)uid != Libc.GetUid()))
        {
            failure = ActionResult.NotYours(process, verb); // an unreadable uid is not taken as the user's
        }
        if (failure is null)
        {
            return true;
        }
        pidfd.Dispose();
        return false;
    }

    /// <summary>A pidfd, or the errno that kept it from opening (ENOSYS: the kernel has none;
    /// EPERM: a sandbox's seccomp filter forbids it). Without one, signals go through kill.</summary>
    private readonly record struct PidFd(int Pid, SafeFileHandle? Handle, int Error) : IDisposable
    {
        public static PidFd Open(int pid)
        {
            var fd = Libc.Syscall(SysPidfdOpen, pid, 0, 0, 0);
            return fd >= 0 ? new(pid, new SafeFileHandle((nint)fd, ownsHandle: true), 0) : new(pid, null, Marshal.GetLastPInvokeError());
        }

        /// <summary>Sends <paramref name="signal"/> (0 only checks the process is there): 0, or the errno.</summary>
        public int Signal(int signal)
        {
            // Without a pidfd (kernels before 5.3, some sandboxes) fall back to kill, which leaves a
            // moment for the PID to be reused between the check and the signal.
            var result = Handle is { } fd
                ? Libc.Syscall(SysPidfdSendSignal, (int)fd.DangerousGetHandle(), signal, 0, 0)
                : Libc.Kill(Pid, signal);
            return result == 0 ? 0 : Marshal.GetLastPInvokeError();
        }

        public void Dispose() => Handle?.Dispose();
    }
}
