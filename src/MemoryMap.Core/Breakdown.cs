namespace MemoryMap.Core;

/// <summary>The six top-level areas the map divides total RAM into, in display order.</summary>
public enum MemoryGroup
{
    Apps,

    /// <summary>Services and system processes ("Windows & services", "System & services").</summary>
    System,
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
/// <param name="Note">What a <see cref="NodeKind.Figure"/> is, in one sentence; otherwise null.</param>
public sealed record MapNode(
    string Key,
    string Name,
    NodeKind Kind,
    MemoryGroup Group,
    long Bytes,
    IReadOnlyList<MapNode> Children,
    ProcessSample? Process = null,
    string? Note = null)
{
    public int ProcessCount => Process is not null ? 1 : Children.Sum(c => c.ProcessCount);

    /// <summary>Every app at or below this node, including apps folded into an "Other".</summary>
    public IEnumerable<MapNode> Apps() => Kind switch
    {
        NodeKind.App => [this],
        NodeKind.Root or NodeKind.Group or NodeKind.Folded => Children.SelectMany(c => c.Apps()),
        _ => [],
    };

    /// <summary>Every process sample at or below this node.</summary>
    public IEnumerable<ProcessSample> Processes() =>
        Process is { } p ? [p] : Children.SelectMany(c => c.Processes());

    public MapNode? Find(string key) => PathTo(key)?[^1];

    /// <summary>The nodes from this one down to the node with <paramref name="key"/>; null if absent.</summary>
    public IReadOnlyList<MapNode>? PathTo(string key)
    {
        if (Key == key)
        {
            return [this];
        }
        foreach (var child in Children)
        {
            if (child.PathTo(key) is { } below)
            {
                return [this, .. below];
            }
        }
        return null;
    }
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
    /// <summary>The key of a process's node: (PID, start time) names one process for good.</summary>
    public static string ProcessKey(ProcessSample p) => $"pid/{p.Pid}/{p.StartTime.UtcTicks}";

    private static string Label(MemoryGroup group, PlatformNames names) => group switch
    {
        MemoryGroup.Apps => "Apps",
        MemoryGroup.System => names.SystemGroup,
        MemoryGroup.VirtualMachines => names.VirtualMachineGroup,
        MemoryGroup.Kernel => "Kernel & drivers",
        MemoryGroup.SharedOther => "Shared & other",
        MemoryGroup.Available => "Available",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown memory group."),
    };

    private static MemoryFigure FreeRest(long bytes) => new("free", "Free", bytes, "Unused RAM.");

    public static MemoryGroup GroupOf(ProcessKind kind) => kind switch
    {
        ProcessKind.App => MemoryGroup.Apps,
        ProcessKind.Service => MemoryGroup.System,
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
        foreach (var group in new[] { MemoryGroup.Apps, MemoryGroup.System, MemoryGroup.VirtualMachines })
        {
            var apps = buckets[(int)group].Values.Select(list => AppNode(group, list)).ToList();
            groups.Add(GroupNode(group, sys.Names, Fold(group, apps, foldBelow)));
        }

        var kernel = sys.Kernel.Select(f => Figure(MemoryGroup.Kernel, f)).ToList();
        groups.Add(GroupNode(MemoryGroup.Kernel, sys.Names, kernel));

        // In use but owned by no process or kernel figure: the OS's named parts first, then the
        // rest (shared pages, mapped files and the like). If the figures overshoot "used" because
        // they were read a moment apart, that overshoot is reported as Skew rather than hidden.
        var attributed = processBytes + kernel.Sum(n => n.Bytes);
        var unattributed = Math.Max(0, sys.Used - attributed);
        groups.Add(GroupNode(MemoryGroup.SharedOther, sys.Names, Carve(MemoryGroup.SharedOther, sys.InUseNamed, unattributed,
            rest => new("shared", sys.Names.SharedRestName, rest, sys.Names.SharedRestNote))));
        groups.Add(GroupNode(MemoryGroup.Available, sys.Names,
            Carve(MemoryGroup.Available, sys.AvailableNamed, sys.Available, FreeRest)));

        var root = new MapNode("root", "Memory", NodeKind.Root, MemoryGroup.Apps, groups.Sum(g => g.Bytes), groups);
        return new MemoryBreakdown(snapshot, root, Math.Max(0, attributed - sys.Used));
    }

    private static MapNode AppNode(MemoryGroup group, List<ProcessSample> processes)
    {
        var children = processes
            .OrderByDescending(p => p.PrivateWorkingSet)
            .Select(p => new MapNode(ProcessKey(p), $"{p.Name} ({p.Pid})",
                NodeKind.Process, group, p.PrivateWorkingSet, [], p))
            .ToList();
        // Processes of one app may differ in image-name casing; pick the label by name, not by
        // size, so it does not flip when a different process becomes the largest.
        var named = processes.MinBy(p => p.ImageName, StringComparer.Ordinal)!;
        return new MapNode($"{group}/{named.ImageName.ToLowerInvariant()}", named.Name, NodeKind.App, group,
            children.Sum(c => c.Bytes), children);
    }

    private static MapNode Figure(MemoryGroup group, MemoryFigure figure) =>
        new($"{group}/{figure.Id}", figure.Name, NodeKind.Figure, group, Math.Max(0, figure.Bytes), [], Note: figure.Note);

    /// <summary>The named figures, each capped at what is left of <paramref name="total"/>, then the rest.</summary>
    private static List<MapNode> Carve(MemoryGroup group, IReadOnlyList<MemoryFigure> named, long total, Func<long, MemoryFigure> rest)
    {
        var nodes = new List<MapNode>(named.Count + 1);
        var left = Math.Max(0, total);
        foreach (var figure in named)
        {
            var bytes = Math.Clamp(figure.Bytes, 0, left);
            nodes.Add(Figure(group, figure with { Bytes = bytes }));
            left -= bytes;
        }
        nodes.Add(Figure(group, rest(left)));
        return nodes;
    }

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

    private static MapNode GroupNode(MemoryGroup group, PlatformNames names, IEnumerable<MapNode> children)
    {
        var nonEmpty = Sorted(children.Where(c => c.Bytes > 0));
        return new MapNode(group.ToString(), Label(group, names), NodeKind.Group, group, nonEmpty.Sum(c => c.Bytes), nonEmpty);
    }

    // Largest first; ties by key so the order is deterministic.
    private static List<MapNode> Sorted(IEnumerable<MapNode> nodes) =>
        nodes.OrderByDescending(n => n.Bytes).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
}
