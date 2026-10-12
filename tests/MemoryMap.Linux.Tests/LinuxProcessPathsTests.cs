using MemoryMap.Core;

namespace MemoryMap.Linux.Tests;

public sealed class LinuxProcessPathsTests
{
    private readonly FakeProcFs _proc = new();

    private static ProcessSample Sample(int pid, long startTicks = 0) =>
        new(pid, "tool", ProcessKind.App, new DateTimeOffset(startTicks, TimeSpan.Zero), 0, 0, 0);

    [Fact]
    public void A_process_reports_the_file_it_runs_even_after_it_was_replaced()
    {
        _proc.Links["10/exe"] = "/opt/tool/tool (deleted)";
        var paths = new LinuxProcessPaths(_proc);

        Assert.Equal("/opt/tool/tool", paths.PathOf(Sample(10)));
        Assert.Null(paths.PathOf(Sample(11))); // another user's process, or a kernel thread
        Assert.Equal(StringComparison.Ordinal, paths.Comparison);
    }

    [Fact]
    public void Each_process_is_asked_once_and_a_reused_pid_again()
    {
        _proc.Links["10/exe"] = "/opt/a";
        var paths = new LinuxProcessPaths(_proc);

        paths.PathOf(Sample(10));
        paths.PathOf(Sample(10));
        _proc.Links["10/exe"] = "/opt/b";

        Assert.Equal(1, _proc.Reads["10/exe"]);
        Assert.Equal("/opt/b", paths.PathOf(Sample(10, startTicks: 99)));
    }

    [Fact]
    public void A_process_that_exec_s_another_program_is_asked_again()
    {
        _proc.Links["10/exe"] = "/bin/sh";
        var paths = new LinuxProcessPaths(_proc);
        paths.PathOf(Sample(10));
        _proc.Links["10/exe"] = "/opt/tool/tool";

        Assert.Equal("/opt/tool/tool", paths.PathOf(Sample(10) with { ImageName = "tool2" }));
    }

    [Fact]
    public void A_process_that_would_not_say_is_asked_again()
    {
        var paths = new LinuxProcessPaths(_proc);
        Assert.Null(paths.PathOf(Sample(12)));
        _proc.Links["12/exe"] = "/opt/late";

        Assert.Equal("/opt/late", paths.PathOf(Sample(12)));
    }
}
