using MemoryMap.Core;
using static MemoryMap.Core.Tests.TestData;

namespace MemoryMap.Core.Tests;

public class StartupMatchingTests
{
    private sealed class Paths(Dictionary<int, string> byPid) : IProcessPaths
    {
        public string? PathOf(ProcessSample process) => byPid.GetValueOrDefault(process.Pid);

        public StringComparison Comparison => StringComparison.OrdinalIgnoreCase;
    }

    private static StartupEntry Entry(string id, string? exe, string? folder,
        StartupSource source = StartupSource.RunKey, StartupScope scope = StartupScope.User, bool counts = true) =>
        new(id, id, null, exe ?? "", "test", source, scope, true, exe, folder, CountsProcesses: counts);

    [Fact]
    public void A_launcher_entry_counts_the_app_it_started_from_the_same_folder()
    {
        StartupEntry[] entries = [Entry("ea", @"C:\EA\Launcher.exe", @"C:\EA")];
        var paths = new Paths(new() { [1] = @"C:\EA\Desktop\EADesktop.exe", [2] = @"C:\Other\x.exe" });

        var usage = StartupMatching.Match(entries, [Proc(1, "EADesktop", 300), Proc(2, "x", 50)], paths);

        Assert.Equal([1], usage["ea"].Processes.Select(p => p.Pid));
        Assert.Equal(300 * Mb, usage["ea"].PrivateWorkingSet);
    }

    [Fact]
    public void A_program_in_a_shared_folder_counts_only_its_own_file()
    {
        StartupEntry[] entries = [Entry("security", @"C:\Windows\System32\SecurityHealthSystray.exe", null)];
        var paths = new Paths(new()
        {
            [1] = @"C:\Windows\System32\SecurityHealthSystray.exe",
            [2] = @"C:\Windows\System32\svchost.exe",
        });

        var usage = StartupMatching.Match(entries, [Proc(1, "SecurityHealthSystray", 5), Proc(2, "svchost", 40)], paths);

        Assert.Equal([1], usage["security"].Processes.Select(p => p.Pid));
    }

    [Fact]
    public void A_process_counts_once_for_the_most_specific_entry()
    {
        StartupEntry[] entries =
        [
            Entry("suite", @"C:\Adobe\Suite.exe", @"C:\Adobe"),
            Entry("ccx", @"C:\Adobe\CCX\CCXProcess.exe", @"C:\Adobe\CCX"),
            Entry("helper", @"C:\Adobe\CCX\Helper.exe", @"C:\Adobe\CCX"),
        ];
        var paths = new Paths(new()
        {
            [1] = @"C:\Adobe\CCX\node.exe",
            [2] = @"c:\adobe\ccx\helper.exe", // case differs: Windows paths ignore it
            [3] = @"C:\Adobe\Suite.exe",
        });

        var usage = StartupMatching.Match(entries, [Proc(1, "node", 10), Proc(2, "Helper", 20), Proc(3, "Suite", 30)], paths);

        Assert.Equal([3], usage["suite"].Processes.Select(p => p.Pid));
        Assert.Equal([1], usage["ccx"].Processes.Select(p => p.Pid)); // first of the two equally deep folders
        Assert.Equal([2], usage["helper"].Processes.Select(p => p.Pid)); // its exact file beats the folder
    }

    [Fact]
    public void A_folder_does_not_claim_a_sibling_whose_name_starts_the_same()
    {
        StartupEntry[] entries = [Entry("app", @"C:\Apps\Tool\tool.exe", @"C:\Apps\Tool")];
        var paths = new Paths(new() { [1] = @"C:\Apps\Toolbox\toolbox.exe" });

        var usage = StartupMatching.Match(entries, [Proc(1, "toolbox", 10)], paths);

        Assert.False(usage["app"].Running);
    }

    [Fact]
    public void Processes_whose_path_is_unknown_are_left_out_and_every_entry_gets_a_result()
    {
        StartupEntry[] entries = [Entry("a", @"C:\A\a.exe", @"C:\A"), Entry("missing", null, null)];
        var paths = new Paths(new()); // protected or exited: Windows would not say

        var usage = StartupMatching.Match(entries, [Proc(1, "a", 10)], paths);

        Assert.False(usage["a"].Running);
        Assert.Same(StartupUsage.None, usage["missing"]);
    }

    [Fact]
    public void An_entry_started_through_a_host_program_is_credited_with_none_of_its_processes()
    {
        StartupEntry[] entries = [Entry("script", @"C:\Windows\System32\cmd.exe", null, counts: false)];
        var paths = new Paths(new() { [1] = @"C:\Windows\System32\cmd.exe" }); // perhaps a window the user opened

        var usage = StartupMatching.Match(entries, [Proc(1, "cmd", 5)], paths);

        Assert.False(usage["script"].Running);
    }

    [Fact]
    public void A_repeated_id_does_not_break_the_match()
    {
        StartupEntry[] entries = [Entry("a", @"C:\A\a.exe", @"C:\A"), Entry("a", @"C:\A\a.exe", @"C:\A")];
        var paths = new Paths(new() { [1] = @"C:\A\a.exe" });

        var usage = StartupMatching.Match(entries, [Proc(1, "a", 10)], paths);

        Assert.Equal([1], usage["a"].Processes.Select(p => p.Pid));
    }

    [Theory]
    [InlineData(StartupSource.RunKey, StartupScope.User, StartupSwitch.Direct)]
    [InlineData(StartupSource.StartupFolder, StartupScope.User, StartupSwitch.Direct)]
    [InlineData(StartupSource.RunKey, StartupScope.Machine, StartupSwitch.NeedsAdmin)]
    [InlineData(StartupSource.StartupFolder, StartupScope.Machine, StartupSwitch.NeedsAdmin)]
    [InlineData(StartupSource.StoreTask, StartupScope.User, StartupSwitch.OsSettings)]
    public void How_an_entry_is_switched_follows_where_it_is_registered(StartupSource source, StartupScope scope, StartupSwitch expected) =>
        Assert.Equal(expected, Entry("x", null, null, source, scope).Switch);

    [Fact]
    public void The_status_says_whether_it_runs_and_how_much_it_holds()
    {
        var entry = Entry("tool", @"C:\Tool\Tool.exe", @"C:\Tool");
        var two = new StartupUsage([Proc(1, "Tool", 300), Proc(2, "Tool", 100)]);

        Assert.Equal("Not running", StartupText.Status(entry, StartupUsage.None));
        Assert.Equal("", StartupText.Memory(entry, StartupUsage.None));
        Assert.Equal("Running", StartupText.Status(entry, new StartupUsage([Proc(1, "Tool", 300)])));
        Assert.Equal("Running, 2 processes", StartupText.Status(entry, two));
        Assert.Equal(Format.Bytes(400 * Mb), StartupText.Memory(entry, two));
        var host = Entry("cmd", @"C:\Windows\System32\cmd.exe", null, counts: false);
        Assert.Equal(StartupText.NotCounted, StartupText.Status(host, two));
        Assert.Equal("", StartupText.Memory(host, two));
    }
}
