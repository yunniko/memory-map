using System.Runtime.Versioning;
using MemoryMap.Core;
using MemoryMap.Windows.Startup;
using static MemoryMap.Windows.Tests.StartupFixture;

namespace MemoryMap.Windows.Tests;

// Every switch here writes to a stand-in registry key (StartupFixture), never the real entries;
// scripts/startup-roundtrip.ps1 checks the same code against Windows Settings on throwaway entries.
[SupportedOSPlatform("windows")]
public sealed class StartupActionsTests : IDisposable
{
    private readonly StartupFixture _pc = new();

    public void Dispose() => _pc.Dispose();

    private StartupEntry Only(string id) => Assert.Single(_pc.Reader().Read().Entries, e => e.Id == id);

    private byte[]? Flag(string approvalPath, string name)
    {
        using var key = _pc.User.OpenSubKey(approvalPath);
        return key?.GetValue(name) as byte[];
    }

    [Fact]
    public void A_run_value_switches_off_and_back_on_and_its_registration_is_untouched()
    {
        var tool = _pc.Program("Vendor", "Tool.exe");
        Set(_pc.User, Run, "Tool", $"\"{tool}\" --tray");
        var before = DateTimeOffset.Now;

        var off = _pc.Actions().SetEnabled(Only("run/user/Tool"), false);

        Assert.True(off.Succeeded, off.Message);
        Assert.Contains("will no longer start", off.Message);
        Assert.False(Only("run/user/Tool").Enabled);
        var flag = Flag(StartupApproval.Run, "Tool")!;
        Assert.Equal([1, 0, 0, 0], flag[..4]);
        var when = DateTimeOffset.FromFileTime(BitConverter.ToInt64(flag, 4));
        Assert.InRange(when, before.AddSeconds(-1), DateTimeOffset.Now.AddSeconds(1));

        var on = _pc.Actions().SetEnabled(Only("run/user/Tool"), true);

        Assert.True(on.Succeeded, on.Message);
        Assert.True(Only("run/user/Tool").Enabled);
        Assert.Equal(new byte[12], Flag(StartupApproval.Run, "Tool"));
        using var run = _pc.User.OpenSubKey(Run)!;
        Assert.Equal($"\"{tool}\" --tray", run.GetValue("Tool"));
    }

    [Fact]
    public void A_startup_folder_file_switches_by_its_file_name_and_stays_in_the_folder()
    {
        var lnk = Path.Combine(_pc.UserFolder, "Tool.lnk");
        Link(lnk, _pc.Program("Vendor", "Tool.exe"));

        Assert.True(_pc.Actions().SetEnabled(Only("folder/user/Tool.lnk"), false).Succeeded);

        Assert.False(Only("folder/user/Tool.lnk").Enabled);
        Assert.Equal(1, Flag(StartupApproval.Folder, "Tool.lnk")![0]);
        Assert.True(File.Exists(lnk));
    }

    [Fact]
    public void An_entry_removed_since_the_read_is_reported_and_gets_no_flag()
    {
        Set(_pc.User, Run, "Tool", _pc.Program("Vendor", "Tool.exe"));
        var entry = Only("run/user/Tool");
        using (var run = _pc.User.OpenSubKey(Run, writable: true)!)
        {
            run.DeleteValue("Tool");
        }

        var result = _pc.Actions().SetEnabled(entry, false);

        Assert.Equal(ActionOutcome.AlreadyExited, result.Outcome);
        Assert.Null(Flag(StartupApproval.Run, "Tool"));
    }

    [Fact]
    public void Machine_entries_and_store_tasks_are_refused_without_writing()
    {
        var tool = _pc.Program("Vendor", "Tool.exe");
        Set(_pc.Machine, Run, "Tool", tool);
        var machine = Only("run/machine/Tool");
        var store = machine with { Id = "task/Fake_1/Task", Source = StartupSource.StoreTask, Scope = StartupScope.User };

        Assert.Equal(ActionOutcome.Refused, _pc.Actions().SetEnabled(machine, false).Outcome);
        Assert.Equal(ActionOutcome.Refused, _pc.Actions().SetEnabled(store, false).Outcome);
        Assert.True(Only("run/machine/Tool").Enabled);
        using var flags = _pc.Machine.OpenSubKey(StartupApproval.Run);
        Assert.Null(flags?.GetValue("Tool"));
    }

    [Fact]
    public void Open_file_location_shows_the_program_else_the_startup_file_else_nothing()
    {
        var tool = _pc.Program("Vendor", "Tool.exe");
        Set(_pc.User, Run, "Tool", tool);
        Set(_pc.User, Run, "Gone", @"C:\No such folder\Gone.exe");
        var broken = Path.Combine(_pc.UserFolder, "Broken.lnk");
        Link(broken, @"C:\No such folder\Broken.exe");
        var actions = _pc.Actions();

        Assert.Equal(tool, actions.FileToShow(Only("run/user/Tool")));
        Assert.Equal(broken, actions.FileToShow(Only("folder/user/Broken.lnk")));
        Assert.Null(actions.FileToShow(Only("run/user/Gone")));
        var result = actions.ShowInExplorer(Only("run/user/Gone"));
        Assert.False(result.Succeeded);
        Assert.Contains("missing", result.Message);
    }

    [Theory]
    [InlineData("run/user/Tool", StartupSource.RunKey, "user", "Tool")]
    [InlineData("run/machine32/A/B", StartupSource.RunKey, "machine32", "A/B")]
    [InlineData("folder/machine/Tool.lnk", StartupSource.StartupFolder, "machine", "Tool.lnk")]
    public void Ids_round_trip(string id, StartupSource source, string scope, string name)
    {
        Assert.Equal(new StartupTarget(source, scope, name), StartupIds.Parse(id));
        Assert.Equal(id, source == StartupSource.RunKey ? StartupIds.Run(scope, name) : StartupIds.Folder(scope, name));
    }

    [Theory]
    [InlineData("task/Fake_1/Task")]
    [InlineData("run/other/Tool")]
    [InlineData("folder/machine32/Tool.lnk")]
    [InlineData("run/user/")]
    public void Other_ids_are_named_in_the_error(string id) =>
        Assert.Contains(id, Assert.Throws<ArgumentException>(() => StartupIds.Parse(id)).Message);

    [Fact]
    public void The_flags_written_read_back_as_written()
    {
        var now = DateTimeOffset.Now;
        Assert.True(StartupApproval.IsEnabled(StartupApproval.Flag(true, now)));
        Assert.False(StartupApproval.IsEnabled(StartupApproval.Flag(false, now)));
        Assert.Equal(12, StartupApproval.Flag(false, now).Length);
    }
}
