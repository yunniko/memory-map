namespace MemoryMap.Core.Tests;

public class TreemapTests
{
    private static readonly LayoutRect Bounds = new(10, 20, 800, 500);

    [Fact]
    public void Areas_are_proportional_to_weights_and_fill_the_bounds()
    {
        double[] weights = [500, 300, 120, 50, 20, 7, 3];
        var rects = Treemap.Squarify(weights, Bounds);

        var total = weights.Sum();
        for (var i = 0; i < weights.Length; i++)
        {
            Assert.Equal(Bounds.Area * weights[i] / total, rects[i].Area, 6);
        }
        Assert.Equal(Bounds.Area, rects.Sum(r => r.Area), 6);
    }

    [Fact]
    public void Rectangles_stay_inside_the_bounds_and_do_not_overlap()
    {
        var random = new Random(42);
        var weights = Enumerable.Range(0, 60).Select(_ => random.NextDouble() * 100 + 0.1).ToArray();
        var rects = Treemap.Squarify(weights, Bounds);

        const double eps = 1e-6;
        foreach (var r in rects)
        {
            Assert.True(r.X >= Bounds.X - eps && r.Y >= Bounds.Y - eps && r.Right <= Bounds.Right + eps && r.Bottom <= Bounds.Bottom + eps, $"{r} outside {Bounds}");
        }
        for (var i = 0; i < rects.Length; i++)
        {
            for (var j = i + 1; j < rects.Length; j++)
            {
                var overlapW = Math.Min(rects[i].Right, rects[j].Right) - Math.Max(rects[i].X, rects[j].X);
                var overlapH = Math.Min(rects[i].Bottom, rects[j].Bottom) - Math.Max(rects[i].Y, rects[j].Y);
                Assert.False(overlapW > eps && overlapH > eps, $"{rects[i]} overlaps {rects[j]}");
            }
        }
    }

    [Fact]
    public void Sorted_weights_give_reasonably_square_tiles()
    {
        double[] weights = [6, 6, 4, 3, 2, 2, 1];
        var rects = Treemap.Squarify(weights, new LayoutRect(0, 0, 6, 4));

        // The paper's worked example: the worst aspect ratio is under 3.
        Assert.All(rects, r => Assert.True(Math.Max(r.Width / r.Height, r.Height / r.Width) < 3, r.ToString()));
    }

    [Fact]
    public void Zero_weights_and_empty_bounds_give_empty_rectangles()
    {
        var rects = Treemap.Squarify([5, 0, -1, 5], Bounds);
        Assert.Equal(default, rects[1]);
        Assert.Equal(default, rects[2]);
        Assert.Equal(Bounds.Area / 2, rects[0].Area, 6);

        Assert.All(Treemap.Squarify([1, 2], new LayoutRect(0, 0, 0, 100)), r => Assert.Equal(default, r));
        Assert.Empty(Treemap.Squarify([], Bounds));
    }

    [Fact]
    public void Layout_places_nodes_by_key_and_skips_empty_ones()
    {
        MapNode Node(string key, long bytes) => new(key, key, NodeKind.Figure, MemoryGroup.Kernel, bytes, []);
        var order = new StableOrder();
        order.Apply(new Dictionary<string, double> { ["b"] = 10, ["a"] = 9 });

        var tiles = Treemap.Layout([Node("a", 10), Node("b", 9), Node("empty", 0)], Bounds, order);

        Assert.Equal(["b", "a"], tiles.Select(t => t.Node.Key));
        Assert.Equal(Bounds.Area * 10 / 19, tiles.Single(t => t.Node.Key == "a").Rect.Area, 6);
        Assert.Equal(["a", "b"], Treemap.Layout([Node("b", 9), Node("a", 10)], Bounds).Select(t => t.Node.Key));
    }

    [Fact]
    public void Layout_names_a_duplicate_key()
    {
        MapNode Node(string key) => new(key, key, NodeKind.Figure, MemoryGroup.Kernel, 5, []);

        var error = Assert.Throws<ArgumentException>(() => Treemap.Layout([Node("a"), Node("a")], Bounds, new StableOrder()));
        Assert.Contains("'a'", error.Message);
    }

    [Fact]
    public void Non_finite_weights_and_extreme_ratios_give_finite_rectangles()
    {
        var rects = Treemap.Squarify([double.PositiveInfinity, double.NaN, 5], Bounds);
        Assert.Equal(default, rects[0]);
        Assert.Equal(default, rects[1]);
        Assert.Equal(Bounds.Area, rects[2].Area, 6);

        double[] extreme = [10e9, .. Enumerable.Repeat(1.0, 3000)];
        Assert.All(Treemap.Squarify(extreme, Bounds), r =>
            Assert.True(double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height), r.ToString()));
    }

    [Fact]
    public void Inset_never_produces_negative_size()
    {
        var r = new LayoutRect(0, 0, 4, 4).Inset(3, 3, 3, 3);
        Assert.Equal(0, r.Width);
        Assert.Equal(0, r.Height);
    }
}

public class StableOrderTests
{
    [Fact]
    public void First_call_orders_largest_first()
    {
        var order = new StableOrder();
        Assert.Equal(["a", "c", "b"], order.Apply(new Dictionary<string, double> { ["a"] = 10, ["b"] = 2, ["c"] = 5 }));
    }

    [Fact]
    public void Small_swaps_in_size_keep_the_previous_order()
    {
        var order = new StableOrder(1.25);
        order.Apply(new Dictionary<string, double> { ["a"] = 10, ["b"] = 9 });

        // b is now bigger, but by less than 25%: no reshuffle.
        Assert.Equal(["a", "b"], order.Apply(new Dictionary<string, double> { ["a"] = 9, ["b"] = 10 }));
    }

    [Fact]
    public void A_clear_change_reorders()
    {
        var order = new StableOrder(1.25);
        order.Apply(new Dictionary<string, double> { ["a"] = 10, ["b"] = 9, ["c"] = 1 });

        Assert.Equal(["c", "a", "b"], order.Apply(new Dictionary<string, double> { ["a"] = 10, ["b"] = 9, ["c"] = 50 }));
    }

    [Fact]
    public void New_keys_are_inserted_by_size_and_gone_keys_dropped()
    {
        var order = new StableOrder();
        order.Apply(new Dictionary<string, double> { ["a"] = 10, ["b"] = 5, ["c"] = 1 });

        Assert.Equal(["a", "d", "c"], order.Apply(new Dictionary<string, double> { ["a"] = 10, ["c"] = 1, ["d"] = 6 }));
    }

    [Fact]
    public void Hysteresis_below_one_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new StableOrder(0.9));
}

public class FormatTests
{
    [Fact]
    public void Bytes_switch_to_gigabytes_at_ten_gb_or_on_request()
    {
        Assert.EndsWith(" MB", Format.Bytes(512L * 1024 * 1024));
        Assert.EndsWith(" GB", Format.Bytes(12L * 1024 * 1024 * 1024));
        Assert.EndsWith(" GB", Format.Bytes(512L * 1024 * 1024, preferGb: true));
    }

    [Fact]
    public void Percent_of_zero_whole_is_a_dash() => Assert.Equal("–", Format.Percent(5, 0));
}
