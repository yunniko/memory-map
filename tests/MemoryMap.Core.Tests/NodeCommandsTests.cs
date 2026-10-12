using MemoryMap.Core;
using static MemoryMap.Core.Tests.TestData;

namespace MemoryMap.Core.Tests;

public class NodeCommandsTests
{
    private static readonly MapNode Root = Breakdown.Build(
        Snapshot(Sys(), Proc(1, "chrome", 3000), Proc(2, "chrome", 500), Proc(3, "code", 1500)),
        new BreakdownOptions { FoldBelowFraction = 0 }).Root;

    private static List<string> Headers(MapNode node, bool canGoBack = false, bool processActions = true) =>
        [.. NodeCommands.For(node, canGoBack, processActions).Select(o => o.Header)];

    [Fact]
    public void An_app_with_one_process_can_be_ended_from_its_tile()
    {
        var options = NodeCommands.For(Root.Find("Apps/code.exe")!, canGoBack: true, processActions: true);

        Assert.Equal(["Zoom in", "Back", "End code (PID 3)…", "Open file location"], options.Select(o => o.Header));
        var end = options.Single(o => o.Command == NodeCommand.End);
        Assert.True(end.Enabled);
        Assert.True(end.StartsGroup);
        Assert.Equal(3, end.Target?.Pid);
    }

    [Fact]
    public void An_app_with_several_processes_is_never_ended_whole()
    {
        var end = NodeCommands.For(Root.Find("Apps/chrome.exe")!, false, true).Single(o => o.Command == NodeCommand.End);

        Assert.False(end.Enabled);
        Assert.Null(end.Target);
        Assert.Equal("End process: zoom in to pick one of its 2", end.Header);
    }

    [Fact]
    public void Groups_and_kernel_items_only_navigate()
    {
        Assert.Equal(["Zoom in"], Headers(Root.Find("Apps")!));
        Assert.Empty(Headers(Root.Find("Kernel/paged-pool")!));
        Assert.Equal(["Zoom in"], Headers(Root.Find("Apps/code.exe")!, processActions: false));
    }

    [Fact]
    public void The_hint_promises_only_what_the_menu_has()
    {
        var process = Root.Find("Apps/chrome.exe")!.Children[0];

        Assert.Equal("Click to zoom in · right-click for actions", NodeCommands.Hint(Root.Find("Apps/code.exe")!, true));
        Assert.Equal("Click to zoom in", NodeCommands.Hint(Root.Find("Apps/code.exe")!, false));
        Assert.Equal("Right-click for actions", NodeCommands.Hint(process, true));
        Assert.Null(NodeCommands.Hint(process, false));
        Assert.Null(NodeCommands.Hint(Root.Find("Kernel/paged-pool")!, true));
    }

    [Fact]
    public void Results_and_the_confirm_question_name_the_process_the_same_way()
    {
        var code = Proc(3, "code", 100);

        Assert.Equal("Ended code (PID 3).", ActionResult.Ended(code).Message);
        Assert.Equal("code (PID 3) has already exited.", ActionResult.Exited(code).Message);
        Assert.StartsWith("Windows denied access to end code (PID 3).", ActionResult.Denied(code, "end").Message, StringComparison.Ordinal);
        Assert.Equal(ActionOutcome.Refused, ActionResult.Critical(code).Outcome);
        Assert.Equal("Could not locate code (PID 3): gone", ActionResult.Failed(code, "locate", "gone").Message);
        Assert.Equal(new ConfirmRequest("End code (PID 3)?", "code closes at once, without saving: anything unsaved in it is lost.", "End process"),
            ConfirmRequest.ToEnd(code));
    }
}
