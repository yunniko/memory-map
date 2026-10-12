namespace MemoryMap.Core;

/// <summary>One line of the table view: a group's direct child, as the map shows it.</summary>
public sealed record TableRow(MemoryGroup Group, string GroupLabel, string Key, string Name, string Size, string Share, int Processes, string Pids);

/// <summary>The words shown for a node, in the tooltip and in the table. Same text in both places.</summary>
public static class Describe
{
    private const int MaxPids = 8;

    /// <summary>Title line and detail lines for a tooltip.</summary>
    public static (string Title, IReadOnlyList<string> Lines) Tooltip(MapNode node, long totalRam)
    {
        var lines = new List<string> { $"{Format.SizeShare(node.Bytes, totalRam)} of RAM" };
        var count = node.ProcessCount;
        if (node.Process is { } process)
        {
            lines.Add($"PID {process.Pid} · started {process.StartTime.ToLocalTime():g}");
        }
        else if (count > 0)
        {
            lines.Add(count == 1 ? "1 process" : $"{count} processes");
            lines.Add(Pids(node));
        }
        if (node.Note is { } note)
        {
            lines.Add(note);
        }
        return (node.Name, lines);
    }

    /// <summary>"PID 42" or "PIDs 1, 2, 3 … +5 more"; largest processes first.</summary>
    public static string Pids(MapNode node)
    {
        var list = PidList(node);
        return list.Length == 0 ? "" : $"{(node.ProcessCount == 1 ? "PID" : "PIDs")} {list}";
    }

    /// <summary>"1, 2, 3 … +5 more" without the label, for the table's "PIDs" column.</summary>
    private static string PidList(MapNode node)
    {
        var pids = node.Processes().OrderByDescending(p => p.PrivateWorkingSet).Select(p => p.Pid).ToList();
        var shown = string.Join(", ", pids.Take(MaxPids));
        return pids.Count > MaxPids ? $"{shown} … +{pids.Count - MaxPids} more" : shown;
    }

    public static IReadOnlyList<TableRow> Table(MemoryBreakdown breakdown) =>
    [
        .. breakdown.Groups.SelectMany(g => g.Children.Select(c => new TableRow(
            g.Group, g.Name, c.Key, c.Name, Format.Bytes(c.Bytes), Format.Percent(c.Bytes, breakdown.Total),
            c.ProcessCount, PidList(c)))),
    ];
}
