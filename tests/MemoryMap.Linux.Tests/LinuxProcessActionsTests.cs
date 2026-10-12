using System.Diagnostics;
using MemoryMap.Core;

namespace MemoryMap.Linux.Tests;

/// <summary>
/// The process actions against real processes: a throwaway <c>sleep</c> this test starts (the
/// user's own), init (never ended; located only when another user's) and the test itself. Linux only: they signal through pidfds.
/// </summary>
public sealed class LinuxProcessActionsTests : IDisposable
{
    private readonly List<string> _shown = [];
    private readonly Process? _child;
    private readonly LinuxProcessActions _actions;

    public LinuxProcessActionsTests()
    {
        _actions = new LinuxProcessActions(showFile: path =>
        {
            _shown.Add(path);
            return null;
        });
        if (OperatingSystem.IsLinux())
        {
            _child = Process.Start(new ProcessStartInfo("sleep", "60") { UseShellExecute = false });
        }
    }

    public void Dispose()
    {
        if (_child is { HasExited: false })
        {
            _child.Kill();
        }
        _child?.Dispose();
    }

    private static ProcessSample Sampled(int pid)
    {
        using var sampler = new LinuxMemorySampler();
        return sampler.Sample().Processes.Single(p => p.Pid == pid);
    }

    [Fact]
    public void Ending_the_users_own_process_ends_it()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Signals real processes.");

        var result = _actions.End(Sampled(_child!.Id));

        Assert.Equal(ActionOutcome.Done, result.Outcome);
        Assert.True(_child.WaitForExit(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void A_process_whose_start_time_differs_is_left_alone_as_already_exited()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Signals real processes.");
        var sample = Sampled(_child!.Id);

        var result = _actions.End(sample with { StartTime = sample.StartTime.AddSeconds(1) });

        Assert.Equal(ActionOutcome.AlreadyExited, result.Outcome);
        Assert.False(_child.HasExited);
    }

    [Fact]
    public void Init_is_refused_as_critical_whoever_owns_it()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Signals real processes.");

        Assert.Equal(ActionOutcome.Refused, _actions.End(Sampled(1)).Outcome);
    }

    [Fact]
    public void Another_users_process_is_refused_without_trying()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Reads real processes.");
        // Only locating, which sends no signal: init is the other user's process to hand.
        Assert.SkipWhen(Uid("1") == Uid("self"), "init runs as this user here (root, or a container).");

        var shown = _actions.ShowFileLocation(Sampled(1));

        Assert.Equal(ActionOutcome.AccessDenied, shown.Outcome);
        Assert.Contains("another user", shown.Message);
        Assert.Empty(_shown);
    }

    private static string Uid(string pid) =>
        File.ReadLines($"/proc/{pid}/status").Single(l => l.StartsWith("Uid:", StringComparison.Ordinal)).Split('\t')[1];

    [Fact]
    public void Memory_map_does_not_end_itself()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Signals real processes.");

        Assert.Equal(ActionOutcome.Refused, _actions.End(Sampled(Environment.ProcessId)).Outcome);
    }

    [Fact]
    public void Open_file_location_shows_the_users_own_programs_executable()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Signals real processes.");

        var result = _actions.ShowFileLocation(Sampled(_child!.Id));

        Assert.Equal(ActionOutcome.Done, result.Outcome);
        var path = Assert.Single(_shown);
        Assert.Equal("sleep", Path.GetFileName(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void A_process_that_has_exited_is_reported_as_such()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Signals real processes.");
        var sample = Sampled(_child!.Id);
        _child.Kill();
        _child.WaitForExit();

        Assert.Equal(ActionOutcome.AlreadyExited, _actions.End(sample).Outcome);
        Assert.Equal(ActionOutcome.AlreadyExited, _actions.ShowFileLocation(sample).Outcome);
    }
}
