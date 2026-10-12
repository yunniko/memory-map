namespace MemoryMap.Linux.Tests;

/// <summary>An in-memory /proc: files and links by relative path, counting reads.</summary>
internal sealed class FakeProcFs : IProcFs
{
    public const long Kb = 1024;

    // A real WSL Ubuntu 24.04 meminfo (2026-10-12), trimmed to the lines the sampler reads and a few it must skip.
    public const string MemInfo = """
        MemTotal:        5037560 kB
        MemFree:           67688 kB
        MemAvailable:    2969192 kB
        Buffers:          398580 kB
        Cached:          2359236 kB
        AnonPages:       1267848 kB
        Shmem:            118988 kB
        Slab:             561732 kB
        SReclaimable:     447268 kB
        SUnreclaim:       114464 kB
        KernelStack:        8764 kB
        PageTables:        17084 kB
        SecPageTables:         0 kB
        CommitLimit:     6713084 kB
        Committed_AS:    5552564 kB
        VmallocTotal:   34359738367 kB
        VmallocUsed:      144192 kB
        Percpu:             2448 kB
        HugePages_Total:       0
        Hugepagesize:       2048 kB
        """;

    public const long BootSeconds = 1_760_000_000;

    public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal)
    {
        ["meminfo"] = MemInfo,
        ["uptime"] = "3600.50 7000.00\n",
        ["stat"] = $"cpu  1 2 3 4\nintr 5\nbtime {BootSeconds}\nprocesses 99\n",
    };

    public Dictionary<string, string> Links { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> Reads { get; } = new(StringComparer.Ordinal);

    public string? ReadText(string path)
    {
        Reads[path] = Reads.GetValueOrDefault(path) + 1;
        return Files.GetValueOrDefault(path);
    }

    public string? ReadLink(string path)
    {
        Reads[path] = Reads.GetValueOrDefault(path) + 1;
        return Links.GetValueOrDefault(path);
    }

    public IEnumerable<int> Pids() =>
        Files.Keys.Select(k => k.Split('/')).Where(p => p.Length == 2 && p[1] == "stat").Select(p => int.Parse(p[0])).Distinct().Order();

    /// <summary>Adds a process with the files every user may read; exe and cgroup are optional.</summary>
    public void Add(int pid, string comm, int uid, long anonKb, long rssKb = 0, long swapKb = 0, long startTicks = 500,
        uint flags = 0x400100, string? exe = null, string? cmdline = null, string cgroup = "0::/user.slice/user-1000.slice/session-1.scope\n")
    {
        // Fields 3-22 of stat: state, ppid, pgrp, session, tty, tpgid, flags (9), ..., starttime (22).
        Files[$"{pid}/stat"] = $"{pid} ({comm}) S 1 {pid} {pid} 0 -1 {flags} 10 0 0 0 5 3 0 0 20 0 1 0 {startTicks} 1000 200 18446744073709551615";
        Files[$"{pid}/status"] = $"Name:\t{comm}\nUmask:\t0022\nState:\tS (sleeping)\nUid:\t{uid}\t{uid}\t{uid}\t{uid}\n"
            + $"VmRSS:\t{Math.Max(rssKb, anonKb)} kB\nRssAnon:\t{anonKb} kB\nRssFile:\t100 kB\nRssShmem:\t0 kB\nVmSwap:\t{swapKb} kB\n";
        Files[$"{pid}/cgroup"] = cgroup;
        if (exe is not null)
        {
            Links[$"{pid}/exe"] = exe;
        }
        if (cmdline is not null)
        {
            Files[$"{pid}/cmdline"] = cmdline;
        }
    }
}
