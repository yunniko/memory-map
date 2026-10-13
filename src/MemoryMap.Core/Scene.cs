namespace MemoryMap.Core;

/// <summary>A node drawn as a frame: a header strip (empty when titles are off or there is no
/// room) and its children's tiles inside.</summary>
public sealed record Frame(MapNode Node, LayoutRect Rect, LayoutRect Header);

/// <summary>Everything one view of the map draws, in drawing order.</summary>
public sealed record Scene(MapNode Root, IReadOnlyList<Frame> Frames, IReadOnlyList<Tile> Tiles)
{
    public static readonly Scene Empty = new(new MapNode("root", "Memory", NodeKind.Root, MemoryGroup.Apps, 0, []), [], []);

    /// <summary>The innermost tile under a point, else the frame around it (its header, outline or a
    /// gap between its tiles); null outside every frame and tile.</summary>
    public MapNode? HitTest(double x, double y)
    {
        foreach (var tile in Tiles)
        {
            if (tile.Rect.Contains(x, y))
            {
                return tile.Node;
            }
        }
        return Frames.FirstOrDefault(f => f.Rect.Contains(x, y))?.Node;
    }
}

/// <summary>
/// Turns a node into a two-level scene: each child becomes a frame holding its own children
/// as tiles, or a tile itself when it has none. Works on any node, so drilling into an app
/// arranges its processes the same way the root arranges groups. Keeps one
/// <see cref="StableOrder"/> per parent so tiles stay put across refreshes. Without titles a
/// frame is only an outline, its tiles <see cref="Outline"/> inside its edge.
/// </summary>
public sealed class SceneArranger
{
    private Dictionary<string, StableOrder> _orders = new(StringComparer.Ordinal);

    public double HeaderHeight { get; init; } = 20;

    /// <summary>Space between neighbouring tiles; the surface shows through it.</summary>
    public double Gap { get; init; } = 2;

    /// <summary>Space between an untitled frame's edge and its tiles: room for the drawn outline.</summary>
    public double Outline { get; init; } = 3;

    public Scene Arrange(MapNode root, LayoutRect bounds, bool titles = true)
    {
        var used = new Dictionary<string, StableOrder>(StringComparer.Ordinal);
        var frames = new List<Frame>();
        var tiles = new List<Tile>();

        foreach (var outer in Place(root, bounds, used))
        {
            if (outer.Node.Children.Count == 0)
            {
                tiles.Add(Shrink(outer));
                continue;
            }
            var rect = outer.Rect.Inset(Gap / 2);
            var header = titles && rect.Height >= HeaderHeight * 2 ? HeaderHeight : 0;
            frames.Add(new Frame(outer.Node, rect, rect with { Height = header }));
            var inner = titles ? rect.Inset(0, header, 0, 0) : rect.Inset(Outline);
            tiles.AddRange(Place(outer.Node, inner, used).Select(Shrink));
        }

        _orders = used; // parents no longer shown drop their order
        return new Scene(root, frames, tiles);
    }

    private IEnumerable<Tile> Place(MapNode parent, LayoutRect bounds, Dictionary<string, StableOrder> used)
    {
        // The root's groups keep their fixed display order; everything below follows size.
        if (parent.Kind == NodeKind.Root)
        {
            var rects = Treemap.Squarify([.. parent.Children.Select(c => (double)c.Bytes)], bounds);
            return parent.Children.Select((c, i) => new Tile(c, rects[i])).Where(t => t.Node.Bytes > 0 && t.Rect.Area > 0);
        }
        var order = used[parent.Key] = _orders.GetValueOrDefault(parent.Key) ?? new StableOrder();
        return Treemap.Layout(parent.Children, bounds, order);
    }

    private Tile Shrink(Tile t) => t with { Rect = t.Rect.Inset(Gap / 2) };
}
