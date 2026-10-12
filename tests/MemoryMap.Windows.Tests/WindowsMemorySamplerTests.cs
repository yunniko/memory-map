using System.Diagnostics;
using System.Runtime.Versioning;
using MemoryMap.Core;

namespace MemoryMap.Windows.Tests;

// Integration tests: they sample the real machine, so they assert relationships that must
// hold on any Windows PC rather than exact numbers.
[SupportedOSPlatform("windows")]
public class WindowsMemorySamplerTests : IDisposable
{
    private readonly WindowsMemorySampler _sampler = new();

    public void Dispose() => _sampler.Dispose();

    [Fact]
    public void Totals_are_consistent()
    {
        var s = _sampler.Sample().System;

        Assert.True(s.TotalPhysical > 1L << 30, "less than 1 GB of RAM reported");
        Assert.InRange(s.Available, 1, s.TotalPhysical);
        Assert.InRange(s.Standby, 0, s.Available);
        Assert.True(s.CommitLimit >= s.TotalPhysical / 2);
        Assert.True(s.NonpagedPool > 0 && s.PagedPoolResident > 0);
        Assert.True(s.PagedPoolResident <= s.PagedPoolTotal);
        Assert.True(s.Uptime > TimeSpan.Zero);
    }

    [Fact]
    public void Total_matches_the_installed_memory_reported_by_dotnet()
    {
        var s = _sampler.Sample().System;
        var gc = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

        Assert.InRange(s.TotalPhysical, gc * 0.98, gc * 1.02);
    }

    [Fact]
    public void Own_process_is_sampled_with_its_real_figures()
    {
        using var me = Process.GetCurrentProcess();
        var sample = _sampler.Sample();
        me.Refresh();
        var self = Assert.Single(sample.Processes, p => p.Pid == me.Id);

        Assert.Equal(me.ProcessName, self.Name, ignoreCase: true);
        Assert.Equal(me.SessionId, self.SessionId);
        Assert.InRange(self.PrivateWorkingSet, 1, self.WorkingSet);
        // Working set moves while the test runs: compare loosely.
        Assert.InRange(self.WorkingSet, me.WorkingSet64 / 2, me.WorkingSet64 * 2);
        Assert.InRange(self.PrivateCommit, me.PrivateMemorySize64 / 2, me.PrivateMemorySize64 * 2);
        Assert.Equal(ProcessKind.App, self.Kind);
        Assert.InRange(self.StartTime, me.StartTime.AddSeconds(-2), me.StartTime.AddSeconds(2));
    }

    [Fact]
    public void Process_list_matches_the_framework_process_list()
    {
        var sampled = _sampler.Sample().Processes.Select(p => p.Pid).ToHashSet();
        var framework = Process.GetProcesses().Select(p => p.Id).Where(id => id != 0).ToHashSet();

        // Processes start and exit between the two calls; allow a little churn.
        var common = sampled.Intersect(framework).Count();
        Assert.True(common >= framework.Count * 0.95, $"only {common} of {framework.Count} processes matched");
        Assert.Contains(4, sampled); // "System"
    }

    [Fact]
    public void Private_working_sets_fit_inside_used_memory()
    {
        var snap = _sampler.Sample();
        var sum = snap.Processes.Sum(p => p.PrivateWorkingSet);

        Assert.InRange(sum, 1, snap.System.Used + (snap.System.TotalPhysical / 50));
    }

    [Fact]
    public void Breakdown_of_a_real_sample_covers_total_ram_plus_reported_skew()
    {
        var b = Breakdown.Build(_sampler.Sample());

        Assert.Equal(b.Total + b.Skew, b.Root.Bytes);
        Assert.InRange(b.Skew, 0, b.Total / 50);
    }

    [Fact]
    public void Sampling_is_cheap_enough_for_a_one_second_refresh()
    {
        _sampler.Sample(); // warm up buffers and PDH
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            _sampler.Sample();
        }
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds / 10.0 < 50, $"one sample took {watch.ElapsedMilliseconds / 10.0:N1} ms");
    }

    [Theory]
    [InlineData("chrome.exe", 1, ProcessKind.App)]
    [InlineData("svchost.exe", 0, ProcessKind.Service)]
    [InlineData("vmmemWSL", 0, ProcessKind.VirtualMachine)]
    [InlineData("VMMEM", 1, ProcessKind.VirtualMachine)]
    [InlineData("explorer.exe", 2, ProcessKind.App)]
    public void Kind_uses_vm_name_then_session(string image, int session, ProcessKind expected) =>
        Assert.Equal(expected, WindowsMemorySampler.KindOf(image, session));

    [Fact]
    public void Clock_stamps_the_snapshot()
    {
        var fixedTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        using var sampler = new WindowsMemorySampler(new FixedClock(fixedTime));

        Assert.Equal(fixedTime, sampler.Sample().Taken.ToUniversalTime());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void Sampling_after_dispose_throws()
    {
        var sampler = new WindowsMemorySampler();
        sampler.Dispose();
        Assert.Throws<ObjectDisposedException>(() => sampler.Sample());
    }
}
