using MemoryMap.Core;

namespace MemoryMap.Linux;

/// <summary>
/// Samples memory on Linux from /proc without root: meminfo for the system figures, and each
/// process's stat and status, which every user may read. A process's size is its resident
/// anonymous memory (D026); its kind comes from its account and cgroup (D027).
/// </summary>
public sealed class LinuxMemorySampler : IMemorySampler
{
    // Regular accounts start at 1000 (login.defs' UID_MIN on Debian, Ubuntu, Fedora, Arch);
    // 60000 and up are reserved for system use, and 65534 is "nobody".
    private const int FirstUserId = 1000;
    private const int LastUserId = 59999;

    private static readonly string[] VmPrefixes =
        ["qemu-system-", "qemu-kvm", "vboxheadless", "virtualboxvm", "vmware-vmx", "firecracker", "crosvm", "cloud-hypervisor"];

    // cgroup paths of containers and machines, not of the runtimes' own daemons (docker.service,
    // containerd.service, lxcfs.service stay services): Docker (systemd and cgroupfs drivers),
    // containerd's CRI and nerdctl, Podman, Kubernetes, LXC, and libvirt or nspawn guests.
    private static readonly string[] ContainerMarks =
        ["/docker-", "/docker/", "/cri-containerd-", "/nerdctl-", "/libpod-", "/kubepods", "/lxc/", "/lxc.payload", "/machine.slice/"];

    /// <summary>Linux's names for its groups and the rest of in-use memory (D025).</summary>
    public static readonly PlatformNames Names = new("System & services", "VMs & containers", "Mapped files, cache & other",
        "In use but owned by no single process: shared libraries and mapped files, file cache the system keeps, and the like.");

    private readonly IProcFs _proc;
    private readonly TimeProvider _clock;
    private readonly Lock _lock = new();

    // Name and kind of each process, read once per (pid, start ticks, comm, uid): pid and start
    // name one process, and comm and uid change when it execs or drops privileges after fork.
    private Dictionary<(int Pid, long Start, string Comm, int Uid), (string Image, ProcessKind Kind)> _identities = [];
    private bool _disposed;

    public LinuxMemorySampler(IProcFs? proc = null, TimeProvider? clock = null)
    {
        if (proc is null && !OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The Linux memory sampler reads /proc and runs on Linux only.");
        }
        _proc = proc ?? new ProcFs();
        _clock = clock ?? TimeProvider.System;
    }

    public MemorySnapshot Sample()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var processes = ReadProcesses(ProcClock.BootTime(_proc));
            var system = ReadSystem(processes.Sum(p => p.PrivateWorkingSet));
            return new MemorySnapshot(_clock.GetLocalNow(), system, processes);
        }
    }

    /// <param name="processAnon">The resident anonymous memory of every process sampled.</param>
    private SystemMemory ReadSystem(long processAnon)
    {
        var m = ProcText.Fields(_proc.ReadRequired("meminfo"));
        long Get(string key) => m.GetValueOrDefault(key);
        if (Get("MemTotal") <= 0 || !m.ContainsKey("MemFree"))
        {
            throw new InvalidDataException("/proc/meminfo has no MemTotal or MemFree line.");
        }

        var free = Get("MemFree");
        var available = m.TryGetValue("MemAvailable", out var a) ? a : free; // MemAvailable: kernel 3.14 and later.
        return new SystemMemory(
            Names: Names,
            TotalPhysical: Get("MemTotal"),
            Available: available,
            CommitTotal: Get("Committed_AS"),
            CommitLimit: Get("CommitLimit"),
            // All of these are the kernel's own allocations: the history's kernel line.
            Kernel:
            [
                new("slab", "Kernel slab (unreclaimable)", Get("SUnreclaim"), "Kernel objects the kernel cannot give back: its own allocations.", true),
                new("kernel-stacks", "Kernel stacks", Get("KernelStack"), "One kernel stack per thread.", true),
                new("page-tables", "Page tables", Get("PageTables") + Get("SecPageTables"), "The tables mapping each process's memory to RAM.", true),
                new("vmalloc", "Vmalloc", Get("VmallocUsed"), "Kernel memory allocated page by page, including loaded modules.", true),
                new("percpu", "Per-CPU", Get("Percpu"), "Kernel memory kept once per processor.", true),
            ],
            InUseNamed:
            [
                new("shmem", "Shared memory (tmpfs, shm)", Get("Shmem"),
                    "Files in RAM-backed file systems and memory processes share on purpose; not counted in any process."),
                new("hugepages", "Huge pages (reserved)", Get("HugePages_Total") * Get("Hugepagesize"),
                    "A pool of large pages set aside at boot or by the administrator."),
                // Program memory the kernel counts but no process we can see holds: processes in
                // another PID namespace, such as other WSL distributions or some containers.
                // Shared copy-on-write pages count in every process, so the sum can exceed it: 0 then.
                new("unseen", "Programs not visible here", Get("AnonPages") - processAnon,
                    "Memory of programs in another process namespace, which this view cannot list: some containers, or other WSL distributions."),
            ],
            AvailableNamed:
            [
                new("reclaimable", "Reclaimable cache", available - free,
                    "File and kernel caches Linux hands to programs as soon as they need them."),
            ],
            Uptime: TimeSpan.FromSeconds(ProcText.FirstNumber(_proc.ReadRequired("uptime"))));
    }

    private List<ProcessSample> ReadProcesses(DateTimeOffset bootTime)
    {
        var list = new List<ProcessSample>(400);
        var seen = new Dictionary<(int, long, string, int), (string, ProcessKind)>(_identities.Count);
        foreach (var pid in _proc.Pids())
        {
            // Either file may vanish when the process exits between listing and reading: skip it then.
            if (_proc.ReadText($"{pid}/stat") is not { } statText || ProcText.Stat(statText) is not { } stat
                || stat.IsKernelThread || _proc.ReadText($"{pid}/status") is not { } statusText)
            {
                continue;
            }
            var status = ProcText.Fields(statusText);
            var key = (pid, stat.StartTicks, stat.Comm, ProcText.Uid(status));
            if (!_identities.TryGetValue(key, out var identity))
            {
                identity = Identify(pid, key.Comm, key.Item4);
            }
            seen[key] = identity;

            var anon = status.GetValueOrDefault("RssAnon");
            list.Add(new ProcessSample(
                Pid: pid,
                ImageName: identity.Image,
                Kind: identity.Kind,
                StartTime: ProcClock.StartTime(bootTime, stat.StartTicks),
                PrivateWorkingSet: anon,
                WorkingSet: status.GetValueOrDefault("VmRSS"),
                PrivateCommit: anon + status.GetValueOrDefault("VmSwap")));
        }
        _identities = seen; // drops processes that have exited
        return list;
    }

    private (string Image, ProcessKind Kind) Identify(int pid, string comm, int uid)
    {
        var image = ImageName(pid, comm);
        var cgroup = _proc.ReadText($"{pid}/cgroup") ?? "";
        var kind =
            VmPrefixes.Any(p => image.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            || ContainerMarks.Any(c => cgroup.Contains(c, StringComparison.Ordinal)) ? ProcessKind.VirtualMachine
            : uid is >= FirstUserId and <= LastUserId ? ProcessKind.App
            : ProcessKind.Service;
        return (image, kind);
    }

    /// <summary>
    /// The executable's file name, as Windows names processes: from the exe link where we may
    /// read it (our own processes), else the command name, completed from the command line
    /// when the kernel cut it to 15 characters.
    /// </summary>
    internal string ImageName(int pid, string comm)
    {
        if (_proc.ReadLink($"{pid}/exe") is { Length: > 0 } exe)
        {
            return Path.GetFileName(ProcText.ExePath(exe));
        }
        if (comm.Length == 15 && _proc.ReadText($"{pid}/cmdline") is { Length: > 0 } cmdline)
        {
            // argv0 is a path, which may hold spaces, or a title the process set ("nginx: worker"),
            // whose first word may be the program.
            var argv0 = cmdline.Split('\0')[0];
            var name = Path.GetFileName(argv0.Contains('/') ? argv0 : argv0.Split(' ', ':')[0]);
            if (name.StartsWith(comm, StringComparison.Ordinal))
            {
                return name;
            }
        }
        return comm;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }
    }
}
