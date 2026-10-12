using MemoryMap.Core;
using static MemoryMap.Core.Tests.TestData;

namespace MemoryMap.Core.Tests;

public class NavigationTests
{
    private static MapNode Tree(params ProcessSample[] processes) =>
        Breakdown.Build(Snapshot(Sys(), processes), new BreakdownOptions { FoldBelowFraction = 0 }).Root;

    private static readonly ProcessSample[] Usual = [Proc(1, "chrome", 3000), Proc(2, "chrome", 500), Proc(3, "code", 1500)];

    [Fact]
    public void Starts_at_the_root()
    {
        var root = Tree(Usual);

        Assert.Equal([root], new Navigation().Resolve(root));
    }

    [Fact]
    public void Zooms_into_an_app_and_follows_it_across_refreshes()
    {
        var nav = new Navigation();
        var root = Tree(Usual);
        var chrome = root.Find("Apps/chrome.exe")!;

        Assert.True(nav.ZoomInto(root, chrome));

        var next = Tree([Proc(1, "chrome", 2000), Proc(2, "chrome", 500), Proc(3, "code", 1500)]);
        var path = nav.Resolve(next);
        Assert.Equal(["root", "Apps", "Apps/chrome.exe"], path.Select(n => n.Key));
        Assert.Same(next.Find("Apps/chrome.exe"), path[^1]);
        Assert.Equal(2, nav.Depth);
    }

    [Fact]
    public void Falls_back_to_the_nearest_ancestor_when_the_zoomed_node_goes()
    {
        var nav = new Navigation();
        var root = Tree(Usual);
        nav.ZoomInto(root, root.Find("Apps/chrome.exe")!);

        var path = nav.Resolve(Tree(Proc(3, "code", 1500))); // chrome exited

        Assert.Equal(["root", "Apps"], path.Select(n => n.Key));
        // It stays there: chrome starting again does not pull the view back in.
        Assert.Equal(["root", "Apps"], nav.Resolve(Tree(Usual)).Select(n => n.Key));
    }

    [Fact]
    public void Follows_an_app_that_moves_into_the_other_fold()
    {
        var nav = new Navigation();
        var options = new BreakdownOptions { FoldBelowFraction = 0.01 }; // 1% of 16 GB ≈ 164 MB
        var big = Breakdown.Build(Snapshot(Sys(), Proc(1, "chrome", 3000), Proc(4, "notepad", 400), Proc(5, "calc", 50), Proc(6, "paint", 60)), options).Root;
        nav.ZoomInto(big, big.Find("Apps/notepad.exe")!);

        var small = Breakdown.Build(Snapshot(Sys(), Proc(1, "chrome", 3000), Proc(4, "notepad", 40), Proc(5, "calc", 50), Proc(6, "paint", 60)), options).Root;
        var path = nav.Resolve(small);

        Assert.Equal(["root", "Apps", "Apps/<other>", "Apps/notepad.exe"], path.Select(n => n.Key));
    }

    [Fact]
    public void Back_and_breadcrumb_go_up()
    {
        var nav = new Navigation();
        var root = Tree(Usual);
        nav.ZoomInto(root, root.Find("Apps/chrome.exe")!);

        Assert.True(nav.Back());
        Assert.Equal("Apps", nav.Resolve(root)[^1].Key);
        Assert.True(nav.Back());
        Assert.False(nav.Back());

        nav.ZoomInto(root, root.Find("Apps/chrome.exe")!);
        nav.ZoomOutTo("Apps");
        Assert.Equal("Apps", nav.Resolve(root)[^1].Key);
        nav.ZoomOutTo("root");
        Assert.Equal(0, nav.Depth);
    }

    [Fact]
    public void Leaves_and_the_root_cannot_be_zoomed_into()
    {
        var nav = new Navigation();
        var root = Tree(Usual);
        var process = root.Find("Apps/chrome.exe")!.Children[0];

        Assert.False(nav.ZoomInto(root, process));
        Assert.False(nav.ZoomInto(root, root.Find("Kernel/nonpaged-pool")!));
        Assert.False(nav.ZoomInto(root, root));
        Assert.Equal(0, nav.Depth);
    }

    [Fact]
    public void Ends_only_a_single_process_and_locates_any_app()
    {
        var root = Tree([.. Usual, Proc(9, "svchost", 300, ProcessKind.Service)]);
        var chrome = root.Find("Apps/chrome.exe")!;
        var code = root.Find("Apps/code.exe")!;

        Assert.Null(ProcessTarget.ToEnd(chrome)); // two processes: zoom in to pick one
        Assert.Equal(1, ProcessTarget.ToEnd(chrome.Children[0])?.Pid);
        Assert.Equal(3, ProcessTarget.ToEnd(code)?.Pid);
        Assert.Equal(1, ProcessTarget.ToLocate(chrome)?.Pid);
        Assert.Null(ProcessTarget.ToEnd(root.Find("Apps")!));
        Assert.Null(ProcessTarget.ToLocate(root.Find("Apps")!));
        Assert.Null(ProcessTarget.ToLocate(root.Find("Kernel/paged-pool")!));
    }

    [Fact]
    public void A_process_tooltip_gives_its_pid_and_start_time()
    {
        var root = Tree(Usual);
        var process = root.Find("Apps/code.exe")!.Children[0];

        var lines = Describe.Tooltip(process, 16 * Gb).Lines;

        Assert.Contains(lines, l => l.StartsWith("PID 3 · started ", StringComparison.Ordinal));
        Assert.DoesNotContain("1 process", lines);
    }
}
