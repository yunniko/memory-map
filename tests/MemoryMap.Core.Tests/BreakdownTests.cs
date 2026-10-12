using static MemoryMap.Core.Tests.TestData;

namespace MemoryMap.Core.Tests;

public class BreakdownTests
{
    private static MapNode Group(MemoryBreakdown b, MemoryGroup group) => b.Groups.Single(g => g.Group == group);

    private static IEnumerable<MapNode> All(MapNode node) => node.Children.SelectMany(All).Prepend(node);

    [Theory]
    [InlineData(ProcessKind.App, MemoryGroup.Apps)]
    [InlineData(ProcessKind.Service, MemoryGroup.Windows)]
    [InlineData(ProcessKind.VirtualMachine, MemoryGroup.VirtualMachines)]
    public void Processes_land_in_the_group_of_their_kind(ProcessKind kind, MemoryGroup expected)
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(10, "x", 500, kind)));

        Assert.Equal("x", Assert.Single(Group(b, expected).Children).Name);
    }

    [Fact]
    public void Groups_appear_in_display_order_and_cover_total_ram()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(10, "chrome", 2000), Proc(11, "svchost", 500, ProcessKind.Service)));

        Assert.Equal(Enum.GetValues<MemoryGroup>(), b.Groups.Select(g => g.Group));
        Assert.Equal(0, b.Skew);
        Assert.Equal(b.Total, b.Root.Bytes);
        Assert.Equal("WSL / Docker VMs", Group(b, MemoryGroup.VirtualMachines).Name);
    }

    [Fact]
    public void Every_node_is_the_sum_of_its_children()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(1, "big", 500), Proc(2, "tiny1", 10), Proc(3, "tiny2", 20),
            Proc(4, "big", 300), Proc(5, "svchost", 50, ProcessKind.Service)));

        Assert.All(All(b.Root).Where(n => n.Children.Count > 0), n => Assert.Equal(n.Bytes, n.Children.Sum(c => c.Bytes)));
        Assert.All(All(b.Root).Where(n => n.Children.Count > 0 && n.Kind != NodeKind.Root),
            n => Assert.Equal(n.Children.OrderByDescending(c => c.Bytes).Select(c => c.Bytes), n.Children.Select(c => c.Bytes)));
        Assert.Equal(5, b.Root.ProcessCount);
    }

    [Fact]
    public void Processes_of_one_app_merge_into_one_node_largest_first()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(10, "chrome", 100), Proc(11, "Chrome", 300), Proc(12, "chrome", 200)));
        var chrome = Assert.Single(Group(b, MemoryGroup.Apps).Children);

        Assert.Equal(NodeKind.App, chrome.Kind);
        Assert.Equal(600 * Mb, chrome.Bytes);
        Assert.Equal([11, 12, 10], chrome.Children.Select(p => p.Process!.Pid));
        Assert.Equal("Apps/chrome.exe", chrome.Key);
        Assert.Equal("Chrome", chrome.Name);
    }

    [Fact]
    public void Keys_are_stable_across_snapshots_and_find_their_node()
    {
        var first = Breakdown.Build(Snapshot(Sys(), Proc(10, "chrome", 100)));
        var second = Breakdown.Build(Snapshot(Sys(availableMb: 5000), Proc(10, "chrome", 900)));

        Assert.Equal(All(first.Root).Select(n => n.Key).Order(), All(second.Root).Select(n => n.Key).Order());
        var process = second.Root.Find(first.Groups[0].Children[0].Children[0].Key);
        Assert.Equal(900 * Mb, process!.Bytes);
        Assert.Null(second.Root.Find("nope"));
    }

    [Fact]
    public void A_reused_pid_is_a_different_process()
    {
        var before = Proc(10, "chrome", 100);
        var after = before with { StartTime = before.StartTime.AddMinutes(5) };

        var keyBefore = Breakdown.Build(Snapshot(Sys(), before)).Groups[0].Children[0].Children[0].Key;
        var keyAfter = Breakdown.Build(Snapshot(Sys(), after)).Groups[0].Children[0].Children[0].Key;
        Assert.NotEqual(keyBefore, keyAfter);
    }

    [Fact]
    public void App_label_does_not_depend_on_which_process_is_largest()
    {
        var a = Breakdown.Build(Snapshot(Sys(), Proc(1, "chrome", 100), Proc(2, "Chrome", 300)));
        var b = Breakdown.Build(Snapshot(Sys(), Proc(1, "chrome", 300), Proc(2, "Chrome", 100)));

        Assert.Equal(a.Groups[0].Children[0].Name, b.Groups[0].Children[0].Name);
    }

    [Fact]
    public void Small_apps_fold_into_one_other_node_per_group()
    {
        // Fold threshold: 0.25% of 16 GB ≈ 41 MB.
        var b = Breakdown.Build(Snapshot(Sys(), Proc(1, "big", 500), Proc(2, "tiny1", 10), Proc(3, "tiny2", 20), Proc(4, "tiny3", 5)));
        var apps = Group(b, MemoryGroup.Apps).Children;

        Assert.Equal(["big", "Other (3 apps)"], apps.Select(i => i.Name));
        var other = apps[1];
        Assert.Equal(NodeKind.Folded, other.Kind);
        Assert.Equal("Apps/<other>", other.Key);
        Assert.Equal(35 * Mb, other.Bytes);
        Assert.Equal(["tiny2", "tiny1", "tiny3"], other.Children.Select(i => i.Name));
        Assert.Equal(3, other.ProcessCount);
    }

    [Fact]
    public void A_single_small_app_is_not_folded()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(1, "big", 500), Proc(2, "tiny", 10)));

        Assert.Equal(["big", "tiny"], Group(b, MemoryGroup.Apps).Children.Select(i => i.Name));
    }

    [Fact]
    public void Shared_and_other_is_used_minus_everything_attributed()
    {
        // Used = 16384 - 4096 = 12288 MB. Attributed = 2000 + 500 processes + 1230 kernel.
        var b = Breakdown.Build(Snapshot(Sys(modifiedMb: 100), Proc(10, "chrome", 2000), Proc(11, "svchost", 500, ProcessKind.Service)));
        var other = Group(b, MemoryGroup.SharedOther);

        Assert.Equal((12288 - 2000 - 500 - 1230) * Mb, other.Bytes);
        Assert.Equal(["SharedOther/shared", "SharedOther/modified"], other.Children.Select(i => i.Key));
        Assert.Equal([(12288 - 2000 - 500 - 1230 - 100) * Mb, 100 * Mb], other.Children.Select(i => i.Bytes));
    }

    [Fact]
    public void Modified_pages_never_exceed_the_unattributed_remainder()
    {
        // Used 1296 MB, kernel 1230 MB: only 66 MB is unattributed, so the modified list
        // (sampled a moment apart, 500 MB) is capped there and the map still sums to total RAM.
        var b = Breakdown.Build(Snapshot(Sys(totalGb: 4, availableMb: 2800, modifiedMb: 500)));
        var item = Assert.Single(Group(b, MemoryGroup.SharedOther).Children);

        Assert.Equal("Modified (waiting to be written)", item.Name);
        Assert.Equal(66 * Mb, item.Bytes);
        Assert.Equal(b.Total, b.Root.Bytes);
    }

    [Fact]
    public void Overshoot_from_sampling_skew_is_reported_not_hidden()
    {
        // Used = 4096 - 3000 = 1096 MB, but processes (2000) and kernel (1230) claim 3230 MB.
        var b = Breakdown.Build(Snapshot(Sys(totalGb: 4, availableMb: 3000), Proc(10, "huge", 2000)));

        Assert.Equal((3230 - 1096) * Mb, b.Skew);
        Assert.Equal(b.Total + b.Skew, b.Root.Bytes);
        Assert.Empty(Group(b, MemoryGroup.SharedOther).Children);
        Assert.All(All(b.Root).Where(n => n.Kind != NodeKind.Group), n => Assert.True(n.Bytes > 0));
    }

    [Fact]
    public void Available_splits_into_standby_and_free()
    {
        var b = Breakdown.Build(Snapshot(Sys(availableMb: 4096, standbyMb: 3000)));

        Assert.Equal([3000 * Mb, 1096 * Mb], Group(b, MemoryGroup.Available).Children.Select(i => i.Bytes));
    }

    [Fact]
    public void Zero_size_processes_are_left_out()
    {
        var b = Breakdown.Build(Snapshot(Sys(), Proc(10, "ghost", 0)));

        Assert.Empty(Group(b, MemoryGroup.Apps).Children);
        Assert.Equal(0, b.Root.ProcessCount);
    }

    [Theory]
    [InlineData("chrome.exe", "chrome")]
    [InlineData("Memory Compression", "Memory Compression")]
    [InlineData("SETUP.EXE", "SETUP")]
    public void Process_name_drops_the_exe_extension(string image, string expected) =>
        Assert.Equal(expected, (Proc(1, "x", 1) with { ImageName = image }).Name);
}
