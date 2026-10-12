using System.Diagnostics;
using System.Runtime.Versioning;
using MemoryMap.Core;

namespace MemoryMap.Windows.Tests;

// Integration tests: they start and end real throwaway processes (ping, which waits on its own).
[SupportedOSPlatform("windows")]
public class WindowsProcessActionsTests
{
    private readonly WindowsProcessActions _actions = new();

    private static Process StartPing()
    {
        var info = new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
        return Process.Start(info) ?? throw new InvalidOperationException("ping did not start");
    }

    private static ProcessSample Sample(Process p, DateTimeOffset? startTime = null) =>
        new(p.Id, "PING.EXE", ProcessKind.App, startTime ?? new DateTimeOffset(p.StartTime), 1, 1, 1);

    [Fact]
    public void Ends_the_sampled_process()
    {
        using var ping = StartPing();

        var result = _actions.End(Sample(ping));

        Assert.Equal(ActionOutcome.Done, result.Outcome);
        Assert.True(ping.WaitForExit(5000), "ping still runs");
    }

    [Fact]
    public void Leaves_a_process_alone_when_its_start_time_differs()
    {
        // The same PID with another start time is what a reused PID looks like.
        using var ping = StartPing();
        try
        {
            var result = _actions.End(Sample(ping, new DateTimeOffset(ping.StartTime).AddSeconds(-5)));

            Assert.Equal(ActionOutcome.AlreadyExited, result.Outcome);
            Assert.False(ping.HasExited);
        }
        finally
        {
            ping.Kill();
        }
    }

    [Fact]
    public void Reports_a_process_that_has_exited()
    {
        using var ping = StartPing();
        var sample = Sample(ping);
        ping.Kill();
        ping.WaitForExit();

        Assert.Equal(ActionOutcome.AlreadyExited, _actions.End(sample).Outcome);
    }

    [Fact]
    public void Does_not_end_a_system_process()
    {
        // PID 4 is the System process. Unelevated, Windows denies access to it. Elevated, the
        // start-time check or the critical-process check stops it; that path is not run here.
        var sample = new ProcessSample(4, "System", ProcessKind.Service, DateTimeOffset.MinValue, 1, 1, 1);

        var result = _actions.End(sample);

        if (Environment.IsPrivilegedProcess)
        {
            Assert.NotEqual(ActionOutcome.Done, result.Outcome);
        }
        else
        {
            Assert.Equal(ActionOutcome.AccessDenied, result.Outcome);
        }
        Assert.Contains("System (PID 4)", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_to_end_itself()
    {
        using var self = Process.GetCurrentProcess();

        Assert.Equal(ActionOutcome.Refused, _actions.End(Sample(self)).Outcome);
    }

    [Fact]
    public void Finds_the_executable_of_a_process()
    {
        using var self = Process.GetCurrentProcess();

        var found = WindowsProcessActions.TryImagePath(Sample(self), out var path, out var failure);

        Assert.True(found, failure?.Message);
        Assert.Equal(self.MainModule!.FileName, path, StringComparer.OrdinalIgnoreCase);
    }
}
