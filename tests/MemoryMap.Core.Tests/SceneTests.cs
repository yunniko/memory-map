using static MemoryMap.Core.Tests.TestData;

namespace MemoryMap.Core.Tests;

public class SceneTests
{
    private static readonly LayoutRect Bounds = new(0, 0, 1000, 600);

    private static MemoryBreakdown Sample(long chromeMb = 3000, long codeMb = 1000) =>
        Breakdown.Build(Snapshot(Sys(), Proc(1, "chrome", chromeMb), Proc(2, "chrome", 500), Proc(3, "code", codeMb),
            Proc(4, "svchost", 400, ProcessKind.Service)));

    [Fact]
    public void Root_scene_frames_each_non_empty_group_and_tiles_its_children()
    {
        var b = Sample();
        var scene = new SceneArranger().Arrange(b.Root, Bounds);

        Assert.Equal(b.Groups.Where(g => g.Bytes > 0).Select(g => g.Key), scene.Frames.Select(f => f.Node.Key));
        Assert.Equal(b.Groups.SelectMany(g => g.Children).Select(c => c.Key).Order(), scene.Tiles.Select(t => t.Node.Key).Order());
        Assert.All(scene.Tiles, t => Assert.Contains(scene.Frames, f => Inside(t.Rect, f.Rect)));
    }

    [Fact]
    public void Tile_areas_follow_bytes_within_the_gap_allowance()
    {
        var b = Sample();
        var scene = new SceneArranger { Gap = 0, HeaderHeight = 0 }.Arrange(b.Root, Bounds);

        foreach (var t in scene.Tiles)
        {
            Assert.Equal(Bounds.Area * t.Node.Bytes / b.Root.Bytes, t.Rect.Area, 3);
        }
    }

    [Fact]
    public void Empty_bounds_give_an_empty_scene()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(1, "chrome", 2000)));
        var scene = new SceneArranger().Arrange(b.Root, new LayoutRect(0, 0, 0, 0));

        Assert.Empty(scene.Frames);
        Assert.Empty(scene.Tiles);
    }

    [Fact]
    public void Arranging_a_leafy_node_gives_tiles_only()
    {
        var chrome = Sample().Groups[0].Children.Single(c => c.Name == "chrome");
        var scene = new SceneArranger().Arrange(chrome, Bounds);

        Assert.Empty(scene.Frames);
        Assert.Equal(2, scene.Tiles.Count);
        Assert.All(scene.Tiles, t => Assert.Equal(NodeKind.Process, t.Node.Kind));
    }

    [Fact]
    public void Tiles_keep_their_order_when_sizes_barely_change()
    {
        var arranger = new SceneArranger();
        var first = arranger.Arrange(Sample(chromeMb: 3000, codeMb: 2900).Root, Bounds);
        var second = arranger.Arrange(Sample(chromeMb: 2900, codeMb: 3000).Root, Bounds);

        Assert.Equal(AppOrder(first), AppOrder(second));
    }

    [Fact]
    public void Hit_test_finds_the_tile_then_the_frame_header()
    {
        var scene = new SceneArranger().Arrange(Sample().Root, Bounds);
        var tile = scene.Tiles[0];
        var frame = scene.Frames.First(f => f.Header.Height > 0);

        Assert.Same(tile.Node, scene.HitTest(tile.Rect.X + 1, tile.Rect.Y + 1));
        Assert.Same(frame.Node, scene.HitTest(frame.Header.X + 1, frame.Header.Y + 1));
        Assert.Null(scene.HitTest(-5, -5));
        Assert.Null(Scene.Empty.HitTest(1, 1));
    }

    private static IEnumerable<string> AppOrder(Scene scene) =>
        scene.Tiles.Where(t => t.Node.Group == MemoryGroup.Apps).Select(t => t.Node.Key);

    private static bool Inside(LayoutRect inner, LayoutRect outer) =>
        inner.X >= outer.X - 1e-6 && inner.Y >= outer.Y - 1e-6 && inner.Right <= outer.Right + 1e-6 && inner.Bottom <= outer.Bottom + 1e-6;
}

public class DescribeTests
{
    [Fact]
    public void Tooltip_gives_size_share_processes_and_pids()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(7, "chrome", 1638), Proc(9, "chrome", 410)));
        var (title, lines) = Describe.Tooltip(b.Groups[0].Children[0], b.Total);

        Assert.Equal("chrome", title);
        Assert.Contains("12.5% of RAM", lines[0]);
        Assert.Contains("2 processes", lines);
        Assert.Contains("PIDs 7, 9", lines);
    }

    [Fact]
    public void Long_pid_lists_are_cut_with_a_count()
    {
        var procs = Enumerable.Range(1, 12).Select(i => Proc(i, "svchost", 100 + i, ProcessKind.Service)).ToArray();
        var node = Breakdown.Build(Snapshot(Sys(), procs)).Groups[1].Children[0];

        Assert.Equal("PIDs 12, 11, 10, 9, 8, 7, 6, 5 … +4 more", Describe.Pids(node));
    }

    [Fact]
    public void System_figures_explain_themselves()
    {
        var b = Breakdown.Build(Snapshot(Sys()));
        var standby = b.Root.Find("Available/standby")!;

        Assert.Contains(Describe.Tooltip(standby, b.Total).Lines, l => l.StartsWith("Cached data", StringComparison.Ordinal));
        Assert.Equal("", Describe.Pids(standby));
    }

    [Fact]
    public void Table_lists_every_tile_of_the_map_with_the_same_numbers()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(1, "chrome", 2000), Proc(2, "svchost", 300, ProcessKind.Service)));
        var rows = Describe.Table(b);

        Assert.Equal(b.Groups.SelectMany(g => g.Children).Select(c => c.Key), rows.Select(r => r.Key));
        var chrome = rows.Single(r => r.Name == "chrome");
        Assert.Equal("Apps", chrome.GroupLabel);
        Assert.Equal(Format.Bytes(2000 * Mb), chrome.Size);
        Assert.Equal("1", chrome.Pids);
    }
}

public class LiveMemoryTests
{
    private sealed class ScriptedSampler(params Func<MemorySnapshot>[] script) : IMemorySampler
    {
        private int _next;

        public MemorySnapshot Sample() => script[Math.Min(_next++, script.Length - 1)]();

        public void Dispose()
        {
        }
    }

    [Fact]
    public void A_failed_sample_is_reported_and_keeps_the_last_good_breakdown()
    {
        var good = Snapshot(Sys(), Proc(1, "chrome", 100));
        using var sampler = new ScriptedSampler(() => good, () => throw new InvalidOperationException("counter hiccup"), () => good);
        using var live = new LiveMemory(sampler, TimeSpan.FromSeconds(1));
        Exception? reported = null;
        live.Failed += e => reported = e;

        Assert.True(live.RefreshNow());
        var first = live.Latest;
        Assert.False(live.RefreshNow());
        Assert.Same(first, live.Latest);
        Assert.Equal("counter hiccup", reported?.Message);
        Assert.True(live.RefreshNow());
        Assert.NotSame(first, live.Latest);
    }

    [Fact]
    public async Task The_loop_publishes_breakdowns_until_disposed()
    {
        using var sampler = new ScriptedSampler(() => Snapshot(Sys(), Proc(1, "chrome", 100)));
        var updates = 0;
        var twice = new TaskCompletionSource();
        var live = new LiveMemory(sampler, LiveMemory.MinimumInterval);
        live.Updated += _ =>
        {
            if (Interlocked.Increment(ref updates) == 2)
            {
                twice.TrySetResult();
            }
        };

        live.Start();
        await twice.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        live.Dispose();
        var after = Volatile.Read(ref updates);
        await Task.Delay(600, TestContext.Current.CancellationToken);

        Assert.Equal(after, Volatile.Read(ref updates));
    }

    [Fact]
    public async Task A_throwing_subscriber_does_not_stop_the_loop()
    {
        using var sampler = new ScriptedSampler(() => Snapshot(Sys(), Proc(1, "chrome", 100)));
        var calls = 0;
        var third = new TaskCompletionSource();
        using var live = new LiveMemory(sampler, LiveMemory.MinimumInterval);
        live.Updated += _ =>
        {
            if (Interlocked.Increment(ref calls) == 3)
            {
                third.TrySetResult();
            }
            throw new InvalidOperationException("subscriber bug");
        };

        live.Start();

        await third.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_shorter_interval_samples_at_once_and_a_longer_one_does_not()
    {
        using var sampler = new ScriptedSampler(() => Snapshot(Sys(), Proc(1, "chrome", 100)));
        var updates = 0;
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        using var live = new LiveMemory(sampler, TimeSpan.FromSeconds(60));
        live.Updated += _ => (Interlocked.Increment(ref updates) == 1 ? first : second).TrySetResult();

        live.Start();
        await first.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        live.Interval = TimeSpan.FromSeconds(120);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref updates));

        live.Interval = TimeSpan.FromSeconds(30); // the timer alone would wait 30 s
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void A_disposed_loop_refuses_to_start_or_sample()
    {
        using var sampler = new ScriptedSampler(() => Snapshot(Sys()));
        var live = new LiveMemory(sampler, TimeSpan.FromSeconds(1));
        live.Dispose();
        live.Dispose();

        Assert.Throws<ObjectDisposedException>(live.Start);
        Assert.Throws<ObjectDisposedException>(() => live.RefreshNow());
        Assert.Throws<ObjectDisposedException>(() => live.Interval = TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Intervals_below_the_minimum_are_rejected()
    {
        using var sampler = new ScriptedSampler(() => Snapshot(Sys()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LiveMemory(sampler, TimeSpan.FromMilliseconds(10)));
        using var live = new LiveMemory(sampler, TimeSpan.FromSeconds(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => live.Interval = TimeSpan.Zero);
        live.Interval = TimeSpan.FromSeconds(2);
        Assert.Equal(TimeSpan.FromSeconds(2), live.Interval);
    }
}
