using System.Globalization;

namespace MemoryMap.Core;

/// <summary>One line of the history chart. <paramref name="Values"/> line up with the chart's times.</summary>
/// <param name="Slot">Which colour of the series palette it wears; see <see cref="SeriesSlots"/>.</param>
public sealed record ChartSeries(string Key, string Name, int Slot, IReadOnlyList<long> Values);

public sealed record ChartTick<T>(T At, string Label);

/// <summary>
/// The history chart for one time window, as data and geometry: which lines, their values, the
/// axis ticks, and where a time or value lands in a given size. One y-axis, in bytes. The control
/// only draws this, so what is drawn can be tested without a window (D007).
/// </summary>
public sealed class HistoryChart
{
    public const int TopApps = 5;
    public const string UsedKey = "used";
    public const string KernelKey = "kernel-pools";

    private const long Mib = 1024 * 1024;

    /// <summary>A longer pause between points (sleep, failed samples) breaks the lines: the chart
    /// does not draw values nobody measured. Three times the slowest refresh.</summary>
    public static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(15);

    /// <summary>The windows the user can pick, in minutes; the history keeps the longest.</summary>
    public static readonly IReadOnlyList<int> WindowChoices = [5, 15, 30, 60];

    private HistoryChart(DateTimeOffset from, TimeSpan window, IReadOnlyList<HistoryPoint> points,
        IReadOnlyList<ChartSeries> series, long yMax)
    {
        From = from;
        Window = window;
        Points = points;
        Series = series;
        YMax = yMax;
    }

    public DateTimeOffset From { get; }

    public TimeSpan Window { get; }

    public DateTimeOffset To => From + Window;

    public IReadOnlyList<HistoryPoint> Points { get; }

    /// <summary>In use, kernel pools, then the window's biggest apps, biggest first.</summary>
    public IReadOnlyList<ChartSeries> Series { get; }

    /// <summary>The top of the y-axis: a round size just above the largest value.</summary>
    public long YMax { get; }

    public bool IsEmpty => Points.Count == 0;

    /// <summary>The chart of the <paramref name="window"/> ending at the newest point.</summary>
    public static HistoryChart Build(MemoryHistory history, TimeSpan window, SeriesSlots slots)
    {
        var to = history.Newest?.Taken ?? DateTimeOffset.UnixEpoch;
        var points = history.Since(to - window);

        // The window's biggest apps by average size: a brief spike does not push out a steady giant.
        var totals = new Dictionary<int, long>();
        foreach (var point in points)
        {
            foreach (var (id, bytes) in point.Apps())
            {
                totals[id] = totals.GetValueOrDefault(id) + bytes;
            }
        }
        var top = totals.OrderByDescending(t => t.Value).ThenBy(t => history.App(t.Key).Key, StringComparer.Ordinal)
            .Take(TopApps).Select(t => t.Key).ToList();
        var assigned = slots.Assign([.. top.Select(id => history.App(id).Key)]);

        List<ChartSeries> series =
        [
            new(UsedKey, "In use", SeriesSlots.Used, [.. points.Select(p => p.Used)]),
            new(KernelKey, "Kernel pools", SeriesSlots.Kernel, [.. points.Select(p => p.KernelPools)]),
            .. top.Select(id =>
            {
                var (key, name) = history.App(id);
                return new ChartSeries(key, name, assigned[key], [.. points.Select(p => p.AppBytes(id))]);
            }),
        ];
        var max = series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max();
        // 5% headroom, so a line at the maximum is not drawn on top of the frame.
        return new HistoryChart(to - window, window, points, series, NiceCeiling(max + (max / 20)));
    }

    /// <summary>Horizontal position of <paramref name="at"/> in a plot <paramref name="width"/> wide.</summary>
    public double X(DateTimeOffset at, double width) => (at - From) / Window * width;

    /// <summary>Vertical position of <paramref name="bytes"/> in a plot <paramref name="height"/> high (0 at the top).</summary>
    public double Y(long bytes, double height) => height - ((double)bytes / YMax * height);

    /// <summary>The point nearest to <paramref name="x"/>; null when the chart is empty.</summary>
    public int? NearestIndex(double x, double width)
    {
        if (IsEmpty || width <= 0)
        {
            return null;
        }
        var at = From + (Window * Math.Clamp(x / width, 0, 1));
        var best = 0;
        for (var i = 1; i < Points.Count; i++)
        {
            if (Math.Abs((Points[i].Taken - at).Ticks) < Math.Abs((Points[best].Taken - at).Ticks))
            {
                best = i;
            }
        }
        return best;
    }

    /// <summary>True when the lines stop before point <paramref name="index"/> and start again at it.</summary>
    public bool BreaksBefore(int index) => index > 0 && Points[index].Taken - Points[index - 1].Taken > MaxGap;

    /// <summary>Round sizes from 0 to <see cref="YMax"/>, one per step.</summary>
    public IReadOnlyList<ChartTick<long>> YTicks()
    {
        var step = Step(YMax);
        var ticks = new List<ChartTick<long>>();
        for (var v = 0L; v <= YMax; v += step)
        {
            ticks.Add(new(v, v == 0 ? "0" : Format.Bytes(v, preferGb: v >= 1024 * Mib)));
        }
        return ticks;
    }

    /// <summary>Clock times on whole minutes, a few per window.</summary>
    public IReadOnlyList<ChartTick<DateTimeOffset>> XTicks(TimeZoneInfo zone)
    {
        var step = TimeSpan.FromMinutes(Window.TotalMinutes switch { <= 5 => 1, <= 30 => 5, _ => 10 });
        var local = TimeZoneInfo.ConvertTime(From, zone);
        var first = new DateTimeOffset(local.Ticks - (local.Ticks % step.Ticks), local.Offset);
        if (first < local)
        {
            first += step;
        }
        var ticks = new List<ChartTick<DateTimeOffset>>();
        for (var t = first; t <= TimeZoneInfo.ConvertTime(To, zone); t += step)
        {
            ticks.Add(new(t, t.ToString("t", CultureInfo.CurrentCulture)));
        }
        return ticks;
    }

    /// <summary>The tooltip for one point: its time, then every line's value and share of RAM.</summary>
    public (string Title, IReadOnlyList<string> Lines) Describe(int index)
    {
        var point = Points[index];
        return (point.Taken.ToLocalTime().ToString("T", CultureInfo.CurrentCulture),
            [.. Series.Select(s => $"{s.Name}  {Format.SizeShare(s.Values[index], point.Total)}")]);
    }

    // Steps are powers of two in MiB, so the labels read 2.0 GB, 4.0 GB rather than 1.9 GB.
    private static long Step(long max)
    {
        var step = 64 * Mib;
        while (max > step * 5)
        {
            step *= 2;
        }
        return step;
    }

    private static long NiceCeiling(long max)
    {
        var step = Step(max);
        return Math.Max(step, (max + step - 1) / step * step);
    }
}

/// <summary>
/// Gives each app line a palette slot and keeps it while the app stays in the chart, so a line
/// does not change colour when the window or the ranking changes. Colour follows the app, not
/// its rank. Slots <see cref="Used"/> and <see cref="Kernel"/> are fixed.
/// </summary>
public sealed class SeriesSlots
{
    public const int Used = 0;
    public const int Kernel = 1;
    public const int FirstApp = 2;

    private readonly Dictionary<string, int> _slots = new(StringComparer.Ordinal);

    /// <summary>Slots for these apps: kept for apps already shown, freed ones for newcomers.</summary>
    public IReadOnlyDictionary<string, int> Assign(IReadOnlyList<string> keys)
    {
        foreach (var gone in _slots.Keys.Except(keys).ToList())
        {
            _slots.Remove(gone);
        }
        foreach (var key in keys.Where(k => !_slots.ContainsKey(k)))
        {
            _slots[key] = Enumerable.Range(FirstApp, HistoryChart.TopApps).First(s => !_slots.ContainsValue(s));
        }
        return new Dictionary<string, int>(_slots);
    }
}
