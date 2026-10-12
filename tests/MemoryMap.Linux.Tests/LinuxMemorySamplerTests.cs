using MemoryMap.Core;
using static MemoryMap.Linux.Tests.FakeProcFs;

namespace MemoryMap.Linux.Tests;

public class LinuxMemorySamplerTests
{
    private readonly FakeProcFs _proc = new();

    private ProcessSample One(int pid) => Assert.Single(new LinuxMemorySampler(_proc).Sample().Processes, p => p.Pid == pid);

    [Fact]
    public void System_figures_come_from_meminfo()
    {
        var s = new LinuxMemorySampler(_proc).Sample().System;

        Assert.Same(LinuxMemorySampler.Names, s.Names);
        Assert.Equal(5037560 * Kb, s.TotalPhysical);
        Assert.Equal(2969192 * Kb, s.Available);
        Assert.Equal(5552564 * Kb, s.CommitTotal);
        Assert.Equal(6713084 * Kb, s.CommitLimit);
        Assert.Equal([114464 * Kb, 8764 * Kb, 17084 * Kb, 144192 * Kb, 2448 * Kb], s.Kernel.Select(f => f.Bytes));
        Assert.Equal(s.Kernel.Sum(f => f.Bytes), s.KernelAllocations);
        Assert.Equal([118988 * Kb, 0, 1267848 * Kb], s.InUseNamed.Select(f => f.Bytes));
        Assert.Equal((2969192 - 67688) * Kb, Assert.Single(s.AvailableNamed).Bytes);
        Assert.Equal(TimeSpan.FromSeconds(3600.5), s.Uptime);
        Assert.All(s.Kernel.Concat(s.InUseNamed).Concat(s.AvailableNamed), f => Assert.False(string.IsNullOrWhiteSpace(f.Note)));
    }

    [Fact]
    public void Without_MemAvailable_free_memory_is_what_is_available()
    {
        _proc.Files["meminfo"] = string.Join('\n', MemInfo.Split('\n').Where(l => !l.StartsWith("MemAvailable", StringComparison.Ordinal)));

        var s = new LinuxMemorySampler(_proc).Sample().System;

        Assert.Equal(67688 * Kb, s.Available);
        Assert.Equal(0, Assert.Single(s.AvailableNamed).Bytes);
    }

    [Fact]
    public void Huge_page_pool_is_a_named_part_of_used_memory()
    {
        _proc.Files["meminfo"] = MemInfo.Replace("HugePages_Total:       0", "HugePages_Total:       4", StringComparison.Ordinal);

        var s = new LinuxMemorySampler(_proc).Sample().System;

        Assert.Equal(4 * 2048 * Kb, Assert.Single(s.InUseNamed, f => f.Id == "hugepages").Bytes);
    }

    [Fact]
    public void Program_memory_no_listed_process_holds_is_named()
    {
        _proc.Add(3, "a", 1000, anonKb: 1_000_000);
        _proc.Add(4, "b", 1000, anonKb: 200_000);

        var s = new LinuxMemorySampler(_proc).Sample().System;

        Assert.Equal((1267848 - 1_200_000) * Kb, Assert.Single(s.InUseNamed, f => f.Id == "unseen").Bytes);
    }

    [Fact]
    public void A_process_is_sized_by_its_resident_anonymous_memory()
    {
        _proc.Add(42, "firefox", 1000, anonKb: 300_000, rssKb: 450_000, swapKb: 20_000, startTicks: 12_345, exe: "/usr/lib/firefox/firefox");

        var p = One(42);

        Assert.Equal(300_000 * Kb, p.PrivateWorkingSet);
        Assert.Equal(450_000 * Kb, p.WorkingSet);
        Assert.Equal(320_000 * Kb, p.PrivateCommit);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(BootSeconds).AddSeconds(123.45), p.StartTime);
    }

    [Theory]
    [InlineData("/usr/lib/firefox/firefox", "Isolated Web Co", null, "firefox")]
    [InlineData("/usr/bin/code (deleted)", "code", null, "code")]
    [InlineData(null, "gnome-shell", null, "gnome-shell")]
    [InlineData(null, "gsd-screensaver", "/usr/libexec/gsd-screensaver-proxy\0--flag\0", "gsd-screensaver-proxy")]
    [InlineData(null, "nginx: worker p", "nginx: worker process\0", "nginx: worker p")]
    [InlineData(null, "systemd-journal", "/lib/systemd/systemd-journald\0", "systemd-journald")]
    [InlineData(null, "longprocessname", "/opt/My App/bin/longprocessname1\0--x\0", "longprocessname1")]
    public void The_image_name_is_the_executable_file_name_where_it_can_be_read(string? exe, string comm, string? cmdline, string expected)
    {
        _proc.Add(7, comm, 1000, anonKb: 10, exe: exe, cmdline: cmdline);

        Assert.Equal(expected, One(7).ImageName);
    }

    [Theory]
    [InlineData(1000, "0::/user.slice/user-1000.slice/session-2.scope", "bash", ProcessKind.App)]
    [InlineData(1001, "0::/init.scope", "bash", ProcessKind.App)]
    [InlineData(0, "0::/system.slice/ssh.service", "sshd", ProcessKind.Service)]
    [InlineData(33, "0::/system.slice/apache2.service", "apache2", ProcessKind.Service)]
    [InlineData(65534, "0::/system.slice/x.service", "x", ProcessKind.Service)]
    [InlineData(0, "0::/system.slice/docker-0123abcd.scope", "postgres", ProcessKind.VirtualMachine)]
    [InlineData(0, "0::/docker/0123abcd", "postgres", ProcessKind.VirtualMachine)]
    [InlineData(0, "0::/system.slice/docker.service", "dockerd", ProcessKind.Service)]
    [InlineData(0, "0::/system.slice/containerd.service", "containerd", ProcessKind.Service)]
    [InlineData(0, "0::/system.slice/lxcfs.service", "lxcfs", ProcessKind.Service)]
    [InlineData(0, "0::/lxc.payload.web/init.scope", "systemd", ProcessKind.VirtualMachine)]
    [InlineData(1000, "0::/user.slice/user-1000.slice/user@1000.service/user.slice/libpod-9f2c.scope/container", "node", ProcessKind.VirtualMachine)]
    [InlineData(0, "0::/kubepods/burstable/pod1/abc", "pause", ProcessKind.VirtualMachine)]
    [InlineData(0, "0::/machine.slice/machine-qemu\\x2d1.scope/libvirt/emulator", "qemu-kvm", ProcessKind.VirtualMachine)]
    [InlineData(1000, "0::/user.slice/user-1000.slice/session-2.scope", "qemu-system-x86_64", ProcessKind.VirtualMachine)]
    public void A_process_kind_comes_from_its_account_cgroup_and_name(int uid, string cgroup, string comm, ProcessKind expected)
    {
        _proc.Add(9, comm, uid, anonKb: 10, cgroup: cgroup + "\n");

        Assert.Equal(expected, One(9).Kind);
    }

    [Fact]
    public void Kernel_threads_and_processes_that_exit_while_being_read_are_left_out()
    {
        _proc.Add(2, "kthreadd", 0, anonKb: 0, flags: 0x208040);
        _proc.Add(50, "gone", 1000, anonKb: 10);
        _proc.Files.Remove("50/status");
        _proc.Add(51, "garbled", 1000, anonKb: 10);
        _proc.Files["51/stat"] = "51 (garbled";
        _proc.Add(60, "kept", 1000, anonKb: 10);

        Assert.Equal([60], new LinuxMemorySampler(_proc).Sample().Processes.Select(p => p.Pid));
    }

    [Fact]
    public void A_command_name_with_spaces_and_parentheses_is_parsed()
    {
        _proc.Add(8, "a) (b c", 1000, anonKb: 10, startTicks: 777);

        Assert.Equal("a) (b c", One(8).ImageName);
    }

    [Fact]
    public void Name_and_kind_are_read_once_per_process_until_the_pid_is_reused()
    {
        _proc.Add(5, "bash", 1000, anonKb: 10, startTicks: 100, exe: "/usr/bin/bash");
        var sampler = new LinuxMemorySampler(_proc);

        sampler.Sample();
        sampler.Sample();
        Assert.Equal(1, _proc.Reads["5/exe"]);
        Assert.Equal(1, _proc.Reads["5/cgroup"]);

        // Same pid, new process.
        _proc.Add(5, "vim", 1000, anonKb: 10, startTicks: 900, exe: "/usr/bin/vim");
        Assert.Equal("vim", Assert.Single(sampler.Sample().Processes).ImageName);
        Assert.Equal(2, _proc.Reads["5/exe"]);
    }

    [Fact]
    public void A_process_is_identified_again_when_it_execs_or_changes_account()
    {
        // Sampled between fork and exec: still the parent's name.
        _proc.Add(6, "bash", 0, anonKb: 10, startTicks: 100, exe: "/usr/bin/bash");
        var sampler = new LinuxMemorySampler(_proc);
        Assert.Equal(("bash", ProcessKind.Service), Kind(sampler));

        _proc.Add(6, "sshd", 0, anonKb: 10, startTicks: 100, exe: "/usr/sbin/sshd");
        Assert.Equal(("sshd", ProcessKind.Service), Kind(sampler));

        _proc.Add(6, "sshd", 1000, anonKb: 10, startTicks: 100, exe: "/usr/sbin/sshd");
        Assert.Equal(("sshd", ProcessKind.App), Kind(sampler));

        static (string, ProcessKind) Kind(LinuxMemorySampler s) => Assert.Single(s.Sample().Processes) is var p ? (p.ImageName, p.Kind) : default;
    }

    [Fact]
    public void A_meminfo_without_the_totals_is_an_error_not_an_empty_map()
    {
        _proc.Files["meminfo"] = string.Join('\n', MemInfo.Split('\n').Where(l => !l.StartsWith("MemTotal", StringComparison.Ordinal)));

        Assert.Throws<InvalidDataException>(() => new LinuxMemorySampler(_proc).Sample());
    }

    [Fact]
    public void A_snapshot_breaks_down_to_total_ram()
    {
        _proc.Add(10, "firefox", 1000, anonKb: 600_000, exe: "/usr/lib/firefox/firefox");
        _proc.Add(11, "firefox", 1000, anonKb: 200_000, exe: "/usr/lib/firefox/firefox");
        _proc.Add(20, "systemd-journal", 0, anonKb: 15_000, cgroup: "0::/system.slice/systemd-journald.service\n");

        var b = Breakdown.Build(new LinuxMemorySampler(_proc).Sample());

        Assert.Equal(0, b.Skew);
        Assert.Equal(b.Total, b.Root.Bytes);
        Assert.Equal(800_000 * Kb, Assert.Single(b.Groups[0].Children).Bytes);
        Assert.Equal("System & services", b.Groups[1].Name);
    }

    [Fact]
    public void Status_fields_turn_kilobytes_into_bytes_and_keep_the_first_number()
    {
        var f = ProcText.Fields("Name:\tbash\nUid:\t1000\t1001\t1002\t1003\nVmRSS:\t  12 kB\nThreads:\t3\n");

        Assert.Equal(1000, f["Uid"]);
        Assert.Equal(12 * Kb, f["VmRSS"]);
        Assert.Equal(3, f["Threads"]);
        Assert.False(f.ContainsKey("Name"));
    }

    [Fact]
    public void On_linux_the_real_proc_agrees_with_meminfo_and_finds_this_process()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Reads the real /proc.");
        using var sampler = new LinuxMemorySampler();

        var snapshot = sampler.Sample();
        var meminfo = ProcText.Fields(File.ReadAllText("/proc/meminfo"));

        Assert.Equal(meminfo["MemTotal"], snapshot.System.TotalPhysical);
        Assert.InRange(snapshot.System.Available, 1, snapshot.System.TotalPhysical);
        var self = Assert.Single(snapshot.Processes, p => p.Pid == Environment.ProcessId);
        Assert.Equal(Path.GetFileName(Environment.ProcessPath), self.ImageName);
        Assert.InRange(self.PrivateWorkingSet, 1, self.WorkingSet);
        Assert.InRange(self.StartTime, DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now.AddSeconds(5));
        Assert.Contains(snapshot.Processes, p => p.Pid == 1);
        Assert.DoesNotContain(snapshot.Processes, p => p.ImageName == "kthreadd");
    }
}
