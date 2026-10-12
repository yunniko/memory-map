namespace MemoryMap.Core;

/// <summary>
/// Where the map is zoomed: the chain of node keys from the root to the node shown. Each
/// refresh builds a new tree, so the chain is resolved by key every time. If the zoomed node
/// has gone (its last process exited), the view falls back to the nearest ancestor still
/// present and stays there. See D009.
/// </summary>
public sealed class Navigation
{
    private List<string> _keys = [];

    public static bool CanZoomInto(MapNode node) => node.Kind != NodeKind.Root && node.Children.Count > 0;

    /// <summary>
    /// The nodes from the root to the one shown, in this tree. Also re-anchors the chain on what
    /// it found, so after a fallback the view stays on the ancestor (D009).
    /// </summary>
    public IReadOnlyList<MapNode> Resolve(MapNode root)
    {
        // Search by key from the deepest level up: an app can move in and out of a group's
        // "Other" fold between refreshes, so its parents' keys are not fixed.
        for (var i = _keys.Count - 1; i >= 0; i--)
        {
            if (root.PathTo(_keys[i]) is { } path)
            {
                SetPath(path);
                return path;
            }
        }
        _keys = [];
        return [root];
    }

    /// <summary>Shows <paramref name="node"/> if it has children to show; false otherwise.</summary>
    public bool ZoomInto(MapNode root, MapNode node)
    {
        if (!CanZoomInto(node) || root.PathTo(node.Key) is not { } path)
        {
            return false;
        }
        SetPath(path);
        return true;
    }

    /// <summary>Shows an ancestor named by key (a breadcrumb), or the root for "root".</summary>
    public void ZoomOutTo(string key)
    {
        var at = _keys.IndexOf(key);
        _keys = at < 0 ? [] : _keys[..(at + 1)];
    }

    /// <summary>One level up. False when already at the root.</summary>
    public bool Back()
    {
        if (_keys.Count == 0)
        {
            return false;
        }
        _keys.RemoveAt(_keys.Count - 1);
        return true;
    }

    private void SetPath(IReadOnlyList<MapNode> path) => _keys = [.. path.Skip(1).Select(n => n.Key)];
}
