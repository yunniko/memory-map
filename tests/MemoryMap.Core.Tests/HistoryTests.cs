using MemoryMap.Core;
using static MemoryMap.Core.Tests.TestData;

namespace MemoryMap.Core.Tests;

public class HistoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    private static MemoryBreakdown At(TimeSpan offset, long availableMb = 4096, params ProcessSample[] processes) =>
        Breakdown.Build(Snapshot(Sys(availableMb: availableMb), processes.Length > 0 ? processes : [Proc(1, "chrome", 3000), Proc(2, "code", 1500)])
            with
        { Taken = T0 + offset });

    private static MemoryHistory Fill(int seconds, Func<int, MemoryBreakdown> at)
    {
        var history = new MemoryHistory();
        for (var s = 0; s < seconds; s++)
        {
            history.Add(at(s));
        }
        return history;
    }

    [Fact]
    public void Keeps_one_point_a_second_however_fast_the_refresh()
    {
        var history = new MemoryHistory();
        for (var ms = 0; ms < 3000; ms += 250)
        {
            history.Add(At(TimeSpan.FromMilliseconds(ms), availableMb: 4096 - ms));
        }

        Assert.Equal(3, history.Count);
        // Each second keeps its newest sample.
        Assert.Equal(16 * Gb - ((4096 - 2750) * Mb), history.Newest!.Used);
    }

    [Fact]
    public void Drops_points_older_than_the_retention()
    {
        var history = Fill(2 * 3600, s => At(TimeSpan.FromSeconds(s)));

        Assert.True(history.Count <= 3601);
        Assert.Equal(T0 + TimeSpan.FromSeconds((2 * 3600) - 1), history.Newest!.Taken);
        Assert.True(history.Newest.Taken - history[0].Taken <= MemoryHistory.Retention);
    }

    [Fact]
    public void Records_used_kernel_pools_and_every_app_including_folded_ones()
    {
        var history = new MemoryHistory();
        history.Add(At(TimeSpan.Zero, 4096, Proc(1, "chrome", 3000), Proc(2, "chrome", 500), Proc(3, "tiny", 1)));
        var point = history.Newest!;

        Assert.Equal(12 * Gb, point.Used);
        Assert.Equal(900 * Mb, point.KernelPools); // 500 nonpaged + 400 paged resident
        var chrome = Enumerable.Range(0, 2).Single(id => history.App(id).Name == "chrome");
        Assert.Equal(3500 * Mb, point.AppBytes(chrome));
        var tiny = Enumerable.Range(0, 2).Single(id => history.App(id).Name == "tiny"); // folded into "Other" on the map
        Assert.Equal(1 * Mb, point.AppBytes(tiny));
    }

    [Fact]
    public void The_chart_shows_used_kernel_and_the_windows_biggest_apps_on_one_axis()
    {
        var processes = Enumerable.Range(1, 8).Select(i => Proc(i, $"app{i}", i * 100)).ToArray();
        var history = Fill(600, s => At(TimeSpan.FromSeconds(s), 4096, processes));

        var chart = HistoryChart.Build(history, TimeSpan.FromMinutes(5), new SeriesSlots());

        Assert.Equal(["In use", "Kernel pools", "app8", "app7", "app6", "app5", "app4"], chart.Series.Select(s => s.Name));
        Assert.All(chart.Series, s => Assert.Equal(chart.Points.Count, s.Values.Count));
        Assert.InRange(chart.Points.Count, 300, 301);
        Assert.True(chart.YMax >= 12 * Gb);
        Assert.Equal(0, chart.YTicks()[0].At);
        Assert.Equal(chart.YMax, chart.YTicks()[^1].At);
        Assert.Equal(0, chart.Y(chart.YMax, 200));
        Assert.Equal(200, chart.Y(0, 200));
        Assert.Equal(400, chart.X(chart.To, 400), 6);
    }

    [Fact]
    public void Top_apps_are_ranked_by_their_average_so_a_spike_does_not_win()
    {
        // "spiky" is huge for one second; "steady" is bigger on average.
        var history = Fill(300, s => At(TimeSpan.FromSeconds(s), 4096,
            Proc(1, "steady", 1000), Proc(2, "spiky", s == 100 ? 9000 : 10),
            Proc(3, "a", 900), Proc(4, "b", 800), Proc(5, "c", 700), Proc(6, "d", 600)));

        var chart = HistoryChart.Build(history, TimeSpan.FromMinutes(5), new SeriesSlots());

        Assert.Equal(["steady", "a", "b", "c", "d"], chart.Series.Skip(2).Select(s => s.Name));
    }

    [Fact]
    public void A_line_keeps_its_colour_when_the_ranking_changes()
    {
        var slots = new SeriesSlots();

        var first = slots.Assign(["chrome", "code", "teams"]);
        var second = slots.Assign(["code", "slack", "chrome"]); // teams left, slack arrived

        Assert.Equal(first["chrome"], second["chrome"]);
        Assert.Equal(first["code"], second["code"]);
        Assert.Equal(first["teams"], second["slack"]); // the freed slot is reused
        Assert.All(second.Values, s => Assert.InRange(s, SeriesSlots.FirstApp, SeriesSlots.FirstApp + HistoryChart.TopApps - 1));
    }

    [Fact]
    public void Hovering_finds_the_nearest_point_and_describes_every_line()
    {
        var history = Fill(300, s => At(TimeSpan.FromSeconds(s)));
        var chart = HistoryChart.Build(history, TimeSpan.FromMinutes(5), new SeriesSlots());

        Assert.Equal(chart.Points.Count - 1, chart.NearestIndex(1000, 1000));
        Assert.Equal(0, chart.NearestIndex(-50, 1000));
        var (_, lines) = chart.Describe(0);
        Assert.Equal(chart.Series.Count, lines.Count);
        Assert.StartsWith("In use  12.0 GB", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Lines_break_across_a_gap_in_sampling()
    {
        var history = new MemoryHistory();
        foreach (var s in new[] { 0, 1, 2, 60, 61 }) // asleep from 2 s to 60 s
        {
            history.Add(At(TimeSpan.FromSeconds(s)));
        }
        var chart = HistoryChart.Build(history, TimeSpan.FromMinutes(5), new SeriesSlots());

        Assert.Equal([3], Enumerable.Range(0, chart.Points.Count).Where(chart.BreaksBefore));
    }

    [Fact]
    public void An_empty_history_draws_an_empty_chart()
    {
        var chart = HistoryChart.Build(new MemoryHistory(), TimeSpan.FromMinutes(30), new SeriesSlots());

        Assert.True(chart.IsEmpty);
        Assert.Null(chart.NearestIndex(10, 100));
        Assert.True(chart.YMax > 0);
    }

    [Fact]
    public void Time_ticks_fall_on_whole_minutes()
    {
        var history = Fill(1800, s => At(TimeSpan.FromSeconds(s)));
        var chart = HistoryChart.Build(history, TimeSpan.FromMinutes(30), new SeriesSlots());

        var ticks = chart.XTicks(TimeZoneInfo.Utc);

        Assert.InRange(ticks.Count, 6, 7);
        Assert.All(ticks, t => Assert.Equal(0, t.At.Minute % 5));
        Assert.All(ticks, t => Assert.Equal(0, t.At.Second));
    }
}
