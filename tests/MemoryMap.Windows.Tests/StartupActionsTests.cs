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

    private byte[]? MachineFlag(string approvalPath, string name)
    {
        using var key = _pc.Machine.OpenSubKey(approvalPath);
        return key?.GetValue(name) as byte[];
    }

    [Theory]
    [InlineData(Run, "run/machine/Tool")]
    [InlineData(Run32, "run/machine32/Tool")]
    public void A_machine_run_value_is_switched_by_the_elevated_helper_under_the_machine_key(string runKey, string id)
    {
        Set(_pc.Machine, runKey, "Tool", _pc.Program("Vendor", "Tool.exe"));
        var approval = runKey == Run32 ? StartupApproval.Run32 : StartupApproval.Run;

        var off = _pc.Actions().SetEnabled(Only(id), false);

        Assert.True(off.Succeeded, off.Message);
        Assert.Equal([StartupHelper.Verb, id, "off"], Assert.Single(_pc.Launcher.Elevated));
        Assert.Equal(1, MachineFlag(approval, "Tool")![0]);
        Assert.Null(Flag(approval, "Tool"));
        Assert.False(Only(id).Enabled);

        Assert.True(_pc.Actions().SetEnabled(Only(id), true).Succeeded);
        Assert.True(Only(id).Enabled);
        Assert.Equal(new byte[12], MachineFlag(approval, "Tool"));
    }

    [Fact]
    public void A_common_startup_file_is_switched_by_the_elevated_helper()
    {
        var lnk = Path.Combine(_pc.MachineFolder, "Everyone.lnk");
        Link(lnk, _pc.Program("Vendor", "Tool.exe"));

        Assert.True(_pc.Actions().SetEnabled(Only("folder/machine/Everyone.lnk"), false).Succeeded);

        Assert.False(Only("folder/machine/Everyone.lnk").Enabled);
        Assert.Equal(1, MachineFlag(StartupApproval.Folder, "Everyone.lnk")![0]);
        Assert.True(File.Exists(lnk));
    }

    [Fact]
    public void A_declined_administrator_prompt_changes_nothing_and_says_so()
    {
        Set(_pc.Machine, Run, "Tool", _pc.Program("Vendor", "Tool.exe"));
        _pc.Launcher.Declines = true;

        var result = _pc.Actions().SetEnabled(Only("run/machine/Tool"), false);

        Assert.Equal(ActionOutcome.Refused, result.Outcome);
        Assert.Contains("declined, so nothing changed", result.Message);
        Assert.True(Only("run/machine/Tool").Enabled);
        Assert.Null(MachineFlag(StartupApproval.Run, "Tool"));
    }

    [Theory]
    [InlineData(0, ActionOutcome.Done)]
    [InlineData(3, ActionOutcome.AlreadyExited)]
    [InlineData(4, ActionOutcome.Failed)]
    [InlineData(-1073741510, ActionOutcome.Failed)]
    public void The_helpers_exit_code_becomes_the_outcome(int exit, ActionOutcome expected)
    {
        Set(_pc.Machine, Run, "Tool", _pc.Program("Vendor", "Tool.exe"));
        _pc.Launcher.ExitCode = exit;

        var result = _pc.Actions().SetEnabled(Only("run/machine/Tool"), false);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(MachineFlag(StartupApproval.Run, "Tool"));
    }

    [Theory]
    [InlineData("--startup-helper", "run/user/Tool", "off")]
    [InlineData("--startup-helper", "folder/user/Tool.lnk", "off")]
    [InlineData("--startup-helper", @"folder/machine/..\..\x.lnk", "off")]
    [InlineData("--startup-helper", "run/machine/Tool", "maybe")]
    [InlineData("--startup-helper", "task/Fake_1/Task", "off")]
    [InlineData("--startup-helper", "run/machine/Tool")]
    public void The_helper_refuses_anything_but_one_machine_entry_and_writes_nothing(params string[] args)
    {
        Set(_pc.User, Run, "Tool", _pc.Program("Vendor", "Tool.exe"));
        Set(_pc.Machine, Run, "Tool", _pc.Program("Vendor", "Tool.exe"));
        Link(Path.Combine(_pc.UserFolder, "Tool.lnk"), _pc.Program("Vendor", "Tool.exe"));

        Assert.Equal(2, StartupHelper.Execute(args, _pc.Locations));

        Assert.Null(Flag(StartupApproval.Run, "Tool"));
        Assert.Null(Flag(StartupApproval.Folder, "Tool.lnk"));
        Assert.Null(MachineFlag(StartupApproval.Run, "Tool"));
        Assert.Null(MachineFlag(StartupApproval.Folder, "x.lnk"));
    }

    [Fact]
    public void Arguments_without_the_helper_verb_start_the_app_normally() =>
        Assert.Null(StartupHelper.Run(["--startup", "run/machine/Tool", "off"]));

    [Fact]
    public void A_store_tasks_switch_opens_settings_and_writes_nothing()
    {
        Set(_pc.Machine, Run, "Tool", _pc.Program("Vendor", "Tool.exe"));
        var store = Only("run/machine/Tool") with { Id = "task/Fake_1/Task", Source = StartupSource.StoreTask, Scope = StartupScope.User };

        var result = _pc.Actions().SetEnabled(store, false);

        Assert.Equal(ActionOutcome.Refused, result.Outcome);
        Assert.Contains("Windows Settings page that just opened", result.Message);
        Assert.Equal([WindowsStartupActions.SettingsPage], _pc.Launcher.Opened);
        Assert.Empty(_pc.Launcher.Elevated);
        Assert.Null(MachineFlag(StartupApproval.Run, "Tool"));
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
        var result = actions.ShowFileLocation(Only("run/user/Gone"));
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
    [InlineData("folder/machine/..")]
    [InlineData(@"folder/user/sub\Tool.lnk")]
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
