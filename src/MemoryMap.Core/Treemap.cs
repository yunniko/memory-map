namespace MemoryMap.Core;

public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Area => Width * Height;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;

    public LayoutRect Inset(double left, double top, double right, double bottom) =>
        new(X + left, Y + top, Math.Max(0, Width - left - right), Math.Max(0, Height - top - bottom));
}

/// <summary>A node placed on the map.</summary>
public readonly record struct Tile(MapNode Node, LayoutRect Rect);

public static class Treemap
{
    /// <summary>
    /// Lays out <paramref name="nodes"/> in <paramref name="bounds"/>. With an
    /// <paramref name="order"/>, tiles keep their places between refreshes; without one they
    /// are laid out largest-first. Nodes of zero size are left out.
    /// </summary>
    public static List<Tile> Layout(IReadOnlyList<MapNode> nodes, LayoutRect bounds, StableOrder? order = null)
    {
        var placed = nodes.Where(n => n.Bytes > 0).ToList();
        if (order is not null)
        {
            var byKey = new Dictionary<string, MapNode>(placed.Count, StringComparer.Ordinal);
            foreach (var node in placed)
            {
                if (!byKey.TryAdd(node.Key, node))
                {
                    throw new ArgumentException($"Two sibling nodes share the key '{node.Key}'; keys must be unique to keep tiles stable.", nameof(nodes));
                }
            }
            placed = [.. order.Apply(byKey.ToDictionary(kv => kv.Key, kv => (double)kv.Value.Bytes, StringComparer.Ordinal)).Select(k => byKey[k])];
        }
        else
        {
            placed = [.. placed.OrderByDescending(n => n.Bytes)];
        }
        var rects = Squarify([.. placed.Select(n => (double)n.Bytes)], bounds);
        return [.. placed.Select((n, i) => new Tile(n, rects[i]))];
    }

    /// <summary>
    /// Squarified treemap (Bruls, Huizing &amp; van Wijk, 2000). Returns one rectangle per weight,
    /// in the same order as <paramref name="weights"/>, together tiling <paramref name="bounds"/>.
    /// Weights are laid out in the order given: pass them largest-first for the squarest tiles,
    /// or in a stable order (see <see cref="StableOrder"/>) for tiles that stay put between frames.
    /// Zero, negative and non-finite weights get an empty rectangle.
    /// </summary>
    public static LayoutRect[] Squarify(IReadOnlyList<double> weights, LayoutRect bounds)
    {
        var result = new LayoutRect[weights.Count];
        var indices = Enumerable.Range(0, weights.Count).Where(i => double.IsFinite(weights[i]) && weights[i] > 0).ToList();
        var total = indices.Sum(i => weights[i]);
        if (indices.Count == 0 || !double.IsFinite(total) || !(bounds.Width > 0) || !(bounds.Height > 0))
        {
            return result;
        }

        var scale = bounds.Area / total;
        double x = bounds.X, y = bounds.Y, w = bounds.Width, h = bounds.Height;
        var next = 0;
        while (next < indices.Count)
        {
            var side = Math.Min(w, h);
            var rowStart = next;
            var rowArea = weights[indices[next]] * scale;
            var rowMin = rowArea;
            var rowMax = rowArea;
            next++;
            while (next < indices.Count)
            {
                var a = weights[indices[next]] * scale;
                var current = Worst(rowArea, rowMin, rowMax, side);
                var candidate = Worst(rowArea + a, Math.Min(rowMin, a), Math.Max(rowMax, a), side);
                if (candidate > current)
                {
                    break;
                }
                rowArea += a;
                rowMin = Math.Min(rowMin, a);
                rowMax = Math.Max(rowMax, a);
                next++;
            }

            // The row runs along the shorter side. "Across" is the row's thickness, "along" the
            // position within it. The last row, and the last tile in a row, take whatever space
            // is left, so rounding never leaves a gap.
            var vertical = w >= h;
            var space = vertical ? w : h;
            var length = vertical ? h : w;
            // Rounding can leave a sliver: give it to this row whole rather than divide by ~0.
            var thickness = next == indices.Count || length <= 1e-9 ? space : Math.Min(space, rowArea / length);
            var start = vertical ? y : x;
            var end = start + length;
            var pos = start;
            for (var k = rowStart; k < next; k++)
            {
                var size = k == next - 1 ? end - pos : thickness > 0 ? weights[indices[k]] * scale / thickness : 0;
                result[indices[k]] = vertical
                    ? new LayoutRect(x, pos, thickness, size)
                    : new LayoutRect(pos, y, size, thickness);
                pos += size;
            }
            if (vertical)
            {
                x += thickness;
                w -= thickness;
            }
            else
            {
                y += thickness;
                h -= thickness;
            }
        }
        return result;
    }

    // Worst aspect ratio in a row of total area `sum` laid along a side of length `side`.
    private static double Worst(double sum, double min, double max, double side)
    {
        var s2 = sum * sum;
        var side2 = side * side;
        return Math.Max(side2 * max / s2, s2 / (side2 * min));
    }
}

/// <summary>
/// Keeps the order of tiles stable across refreshes so the live map does not reshuffle
/// whenever two similar sizes trade places. A tile moves ahead of its neighbour only when
/// it is clearly larger (by the hysteresis factor); new keys are inserted by size.
/// The result stays close to largest-first, which keeps the squarified tiles square.
/// </summary>
public sealed class StableOrder
{
    private readonly double _hysteresis;
    private List<string> _order = [];

    public StableOrder(double hysteresis = 1.25)
    {
        if (hysteresis < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(hysteresis), hysteresis, "Hysteresis must be at least 1.");
        }
        _hysteresis = hysteresis;
    }

    /// <summary>Returns the keys of <paramref name="sizes"/> in display order and remembers it.</summary>
    public IReadOnlyList<string> Apply(IReadOnlyDictionary<string, double> sizes)
    {
        var order = _order.Where(sizes.ContainsKey).ToList();
        var known = order.ToHashSet(StringComparer.Ordinal);
        foreach (var key in sizes.Keys.Where(k => !known.Contains(k)).OrderByDescending(k => sizes[k]).ThenBy(k => k, StringComparer.Ordinal))
        {
            var at = order.FindIndex(k => sizes[k] < sizes[key]);
            order.Insert(at < 0 ? order.Count : at, key);
        }

        // Insertion sort that only moves an item past a neighbour it clearly outgrew.
        for (var i = 1; i < order.Count; i++)
        {
            var j = i;
            while (j > 0 && sizes[order[j]] > sizes[order[j - 1]] * _hysteresis)
            {
                (order[j], order[j - 1]) = (order[j - 1], order[j]);
                j--;
            }
        }

        _order = order;
        return order;
    }
}
