namespace MemoryMap.Core;

/// <summary>
/// One moment of the history: the machine-wide figures and the private working set of every app.
/// App sizes are kept for all apps, not just today's biggest, so the top apps of any window can
/// be chosen after the fact without gaps in their lines.
/// </summary>
public sealed class HistoryPoint
{
    // Sorted by id; sizes in KiB so a point costs 8 bytes per app (D010).
    private readonly int[] _ids;
    private readonly int[] _kib;

    internal HistoryPoint(DateTimeOffset taken, TimeSpan interval, long used, long kernel, long total, int[] ids, int[] kib)
    {
        Taken = taken;
        Interval = interval;
        Used = used;
        Kernel = kernel;
        Total = total;
        _ids = ids;
        _kib = kib;
    }

    public DateTimeOffset Taken { get; }

    /// <summary>The refresh interval in force when this point was taken.</summary>
    public TimeSpan Interval { get; }

    public long Used { get; }

    /// <summary>The kernel's own allocations in RAM (<see cref="SystemMemory.KernelAllocations"/>).</summary>
    public long Kernel { get; }

    public long Total { get; }

    /// <summary>The app's private working set at this moment; 0 when it was not running.</summary>
    public long AppBytes(int id)
    {
        var i = Array.BinarySearch(_ids, id);
        return i < 0 ? 0 : _kib[i] * 1024L;
    }

    /// <summary>Adds every app's KiB to <paramref name="totals"/>, indexed by app id.</summary>
    internal void AddKibTo(long[] totals)
    {
        for (var i = 0; i < _ids.Length; i++)
        {
            totals[_ids[i]] += _kib[i];
        }
    }
}

/// <summary>
/// The recent past, in memory only: at most one point per <see cref="Resolution"/>, for
/// <see cref="Retention"/>. A faster refresh replaces the newest point rather than adding one,
/// so the buffer's size does not depend on the refresh interval (D010). Not thread-safe: the
/// UI thread owns it.
/// </summary>
public sealed class MemoryHistory
{
    public static readonly TimeSpan Resolution = TimeSpan.FromSeconds(1);
    /// <summary>The longest chart window the settings offer.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromMinutes(AppSettings.WindowChoices.Max());

    private readonly HistoryPoint?[] _ring;
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private readonly List<(string Key, string Name)> _apps = [];
    private int _start;
    private int _count;

    public MemoryHistory()
    {
        // A point a second for the retention period, plus the one being replaced.
        _ring = new HistoryPoint?[(int)(Retention / Resolution) + 1];
    }

    public int Count => _count;

    /// <summary>How many distinct apps have been seen; ids run from 0 to this.</summary>
    public int AppCount => _apps.Count;

    public HistoryPoint this[int index] =>
        (uint)index < (uint)_count ? _ring[(_start + index) % _ring.Length]! : throw new ArgumentOutOfRangeException(nameof(index));

    public HistoryPoint? Newest => _count == 0 ? null : this[_count - 1];

    /// <summary>The app's map key and name, for an id from a point.</summary>
    public (string Key, string Name) App(int id) => _apps[id];

    /// <param name="interval">The refresh interval in force, so the chart can tell a pause from
    /// slow sampling; one <see cref="Resolution"/> when not given.</param>
    public void Add(MemoryBreakdown breakdown, TimeSpan? interval = null)
    {
        var sys = breakdown.Snapshot.System;
        // Apps folded into "Other" are still apps: their lines must not vanish when they shrink.
        var apps = breakdown.Root.Apps().Select(a => (Id: IdOf(a), Kib: (int)Math.Min(int.MaxValue, a.Bytes / 1024))).ToList();
        apps.Sort((a, b) => a.Id.CompareTo(b.Id));
        var point = new HistoryPoint(breakdown.Snapshot.Taken, interval ?? Resolution, sys.Used, sys.KernelAllocations,
            sys.TotalPhysical, [.. apps.Select(a => a.Id)], [.. apps.Select(a => a.Kib)]);

        // Same resolution slot as the newest point: replace it. Comparing slots, not the gap to the
        // newest point, keeps a fast refresh from replacing the same point forever.
        if (Newest is { } newest && Slot(point.Taken) == Slot(newest.Taken))
        {
            _ring[(_start + _count - 1) % _ring.Length] = point;
        }
        else
        {
            if (_count == _ring.Length)
            {
                _start = (_start + 1) % _ring.Length;
                _count--;
            }
            _ring[(_start + _count) % _ring.Length] = point;
            _count++;
        }
        while (_count > 0 && point.Taken - this[0].Taken > Retention)
        {
            _ring[_start] = null;
            _start = (_start + 1) % _ring.Length;
            _count--;
        }
    }

    /// <summary>The points taken at or after <paramref name="from"/>, oldest first.</summary>
    public IReadOnlyList<HistoryPoint> Since(DateTimeOffset from)
    {
        // Points are in time order: binary-search the first one in the window.
        var (lo, hi) = (0, _count);
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            (lo, hi) = this[mid].Taken < from ? (mid + 1, hi) : (lo, mid);
        }
        var list = new List<HistoryPoint>(_count - lo);
        for (var i = lo; i < _count; i++)
        {
            list.Add(this[i]);
        }
        return list;
    }

    private static long Slot(DateTimeOffset taken) => taken.UtcTicks / Resolution.Ticks;

    private int IdOf(MapNode app)
    {
        if (!_ids.TryGetValue(app.Key, out var id))
        {
            id = _apps.Count;
            _ids.Add(app.Key, id);
            _apps.Add((app.Key, app.Name));
        }
        return id;
    }
}
