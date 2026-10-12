namespace MemoryMap.Core;

/// <summary>The six top-level areas the map divides total RAM into, in display order.</summary>
public enum MemoryGroup
{
    Apps,
    Windows,
    VirtualMachines,
    Kernel,
    SharedOther,
    Available,
}

public enum NodeKind
{
    Root,
    Group,

    /// <summary>All processes of one executable.</summary>
    App,

    /// <summary>Apps too small to show on their own, folded into one "Other" tile.</summary>
    Folded,
    Process,

    /// <summary>A system-wide figure such as the nonpaged pool or standby cache.</summary>
    Figure,
}

/// <summary>
/// One node of the memory tree: root → groups → apps, "Other" buckets and system figures →
/// processes. Every level has the same shape, so layout, drill-down and hit-testing work on
/// any node. A node's <see cref="Bytes"/> is the sum of its children's.
/// </summary>
/// <param name="Key">Identity that stays the same across refreshes (never a display label).
/// Layout order and colours key on it.</param>
/// <param name="Children">Largest first; the root's groups are in <see cref="MemoryGroup"/> order.</param>
/// <param name="Process">The sample behind a <see cref="NodeKind.Process"/> node; otherwise null.</param>
public sealed record MapNode(
    string Key,
    string Name,
    NodeKind Kind,
    MemoryGroup Group,
    long Bytes,
    IReadOnlyList<MapNode> Children,
    ProcessSample? Process = null)
{
    public int ProcessCount => Process is not null ? 1 : Children.Sum(c => c.ProcessCount);

    /// <summary>Every process sample at or below this node.</summary>
    public IEnumerable<ProcessSample> Processes() =>
        Process is { } p ? [p] : Children.SelectMany(c => c.Processes());

    public MapNode? Find(string key) =>
        Key == key ? this : Children.Select(c => c.Find(key)).FirstOrDefault(n => n is not null);
}

/// <param name="Skew">How far process and kernel figures overshoot used memory because they
/// were read a moment apart. The groups then sum to total RAM plus this; normally 0.</param>
public sealed record MemoryBreakdown(MemorySnapshot Snapshot, MapNode Root, long Skew)
{
    public long Total => Snapshot.System.TotalPhysical;

    public IReadOnlyList<MapNode> Groups => Root.Children;
}

public sealed record BreakdownOptions
{
    /// <summary>Apps smaller than this share of total RAM are folded into one "Other" tile per group.</summary>
    public double FoldBelowFraction { get; init; } = 0.0025;
}

public static class Breakdown
{
    public static string Label(MemoryGroup group) => group switch
    {
        MemoryGroup.Apps => "Apps",
        MemoryGroup.Windows => "Windows & services",
        MemoryGroup.VirtualMachines => "WSL / Docker VMs",
        MemoryGroup.Kernel => "Kernel & drivers",
        MemoryGroup.SharedOther => "Shared & other",
        MemoryGroup.Available => "Available",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown memory group."),
    };

    public static MemoryGroup GroupOf(ProcessKind kind) => kind switch
    {
        ProcessKind.App => MemoryGroup.Apps,
        ProcessKind.Service => MemoryGroup.Windows,
        ProcessKind.VirtualMachine => MemoryGroup.VirtualMachines,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown process kind."),
    };

    public static MemoryBreakdown Build(MemorySnapshot snapshot, BreakdownOptions? options = null)
    {
        options ??= new BreakdownOptions();
        var sys = snapshot.System;
        var foldBelow = (long)(sys.TotalPhysical * options.FoldBelowFraction);

        // One pass: bucket every process into its group, then by executable.
        var buckets = new Dictionary<string, List<ProcessSample>>[3];
        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] = new Dictionary<string, List<ProcessSample>>(StringComparer.OrdinalIgnoreCase);
        }
        long processBytes = 0;
        foreach (var p in snapshot.Processes)
        {
            if (p.PrivateWorkingSet <= 0)
            {
                continue;
            }
            var bucket = buckets[(int)GroupOf(p.Kind)];
            if (!bucket.TryGetValue(p.ImageName, out var list))
            {
                bucket[p.ImageName] = list = [];
            }
            list.Add(p);
            processBytes += p.PrivateWorkingSet;
        }

        var groups = new List<MapNode>();
        foreach (var group in new[] { MemoryGroup.Apps, MemoryGroup.Windows, MemoryGroup.VirtualMachines })
        {
            var apps = buckets[(int)group].Values.Select(list => AppNode(group, list)).ToList();
            groups.Add(GroupNode(group, Fold(group, apps, foldBelow)));
        }

        MapNode[] kernel =
        [
            Figure(MemoryGroup.Kernel, "nonpaged-pool", "Nonpaged pool", sys.NonpagedPool),
            Figure(MemoryGroup.Kernel, "paged-pool", "Paged pool (resident)", sys.PagedPoolResident),
            Figure(MemoryGroup.Kernel, "file-cache", "File cache (resident)", sys.SystemCacheResident),
            Figure(MemoryGroup.Kernel, "driver-code", "Driver code", sys.DriverCodeResident),
            Figure(MemoryGroup.Kernel, "kernel-code", "Kernel code", sys.KernelCodeResident),
        ];
        groups.Add(GroupNode(MemoryGroup.Kernel, kernel));

        // In use but owned by no process or kernel figure: the modified list (named, since
        // Windows counts it), then pages shared between processes (DLLs, browser shared
        // memory), page tables and the like. If the figures overshoot "used" because they
        // were read a moment apart, that overshoot is reported as Skew rather than hidden.
        var attributed = processBytes + kernel.Sum(n => n.Bytes);
        var unattributed = Math.Max(0, sys.Used - attributed);
        var modified = Math.Min(sys.ModifiedPages, unattributed);
        groups.Add(GroupNode(MemoryGroup.SharedOther,
        [
            Figure(MemoryGroup.SharedOther, "shared", "Shared & other", unattributed - modified),
            Figure(MemoryGroup.SharedOther, "modified", "Modified (waiting to be written)", modified),
        ]));

        var standby = Math.Min(sys.Standby, sys.Available);
        groups.Add(GroupNode(MemoryGroup.Available,
        [
            Figure(MemoryGroup.Available, "standby", "Standby cache", standby),
            Figure(MemoryGroup.Available, "free", "Free", sys.Available - standby),
        ]));

        var root = new MapNode("root", "Memory", NodeKind.Root, MemoryGroup.Apps, groups.Sum(g => g.Bytes), groups);
        return new MemoryBreakdown(snapshot, root, Math.Max(0, attributed - sys.Used));
    }

    private static MapNode AppNode(MemoryGroup group, List<ProcessSample> processes)
    {
        var children = processes
            .OrderByDescending(p => p.PrivateWorkingSet)
            .Select(p => new MapNode($"pid/{p.Pid}/{p.StartTime.UtcTicks}", $"{p.Name} ({p.Pid})",
                NodeKind.Process, group, p.PrivateWorkingSet, [], p))
            .ToList();
        // Processes of one app may differ in image-name casing; pick the label by name, not by
        // size, so it does not flip when a different process becomes the largest.
        var named = processes.MinBy(p => p.ImageName, StringComparer.Ordinal)!;
        return new MapNode($"{group}/{named.ImageName.ToLowerInvariant()}", named.Name, NodeKind.App, group,
            children.Sum(c => c.Bytes), children);
    }

    private static MapNode Figure(MemoryGroup group, string id, string name, long bytes) =>
        new($"{group}/{id}", name, NodeKind.Figure, group, Math.Max(0, bytes), []);

    private static List<MapNode> Fold(MemoryGroup group, List<MapNode> apps, long foldBelow)
    {
        var small = apps.Where(a => a.Bytes < foldBelow).ToList();
        if (small.Count < 2)
        {
            return apps;
        }
        var kept = apps.Where(a => a.Bytes >= foldBelow).ToList();
        kept.Add(new MapNode($"{group}/<other>", $"Other ({small.Count} apps)", NodeKind.Folded, group,
            small.Sum(a => a.Bytes), Sorted(small)));
        return kept;
    }

    private static MapNode GroupNode(MemoryGroup group, IEnumerable<MapNode> children)
    {
        var nonEmpty = Sorted(children.Where(c => c.Bytes > 0));
        return new MapNode(group.ToString(), Label(group), NodeKind.Group, group, nonEmpty.Sum(c => c.Bytes), nonEmpty);
    }

    // Largest first; ties by key so the order is deterministic.
    private static List<MapNode> Sorted(IEnumerable<MapNode> nodes) =>
        nodes.OrderByDescending(n => n.Bytes).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
}
