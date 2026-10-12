using System.Runtime.Versioning;
using MemoryMap.Core;
using MemoryMap.Windows.Startup;
using Microsoft.Win32;
using static MemoryMap.Windows.Tests.StartupFixture;

namespace MemoryMap.Windows.Tests;

// Every test reads a stand-in registry key and temp folders (StartupFixture), never the real entries.
[SupportedOSPlatform("windows")]
public sealed class StartupReaderTests : IDisposable
{
    private readonly StartupFixture _pc = new();

    public void Dispose() => _pc.Dispose();

    private StartupEntry Only(string id) => Assert.Single(_pc.Reader().Read().Entries, e => e.Id == id);

    [Fact]
    public void Run_values_are_listed_with_the_state_their_flag_gives()
    {
        var tool = _pc.Program("Vendor", "Tool.exe");
        var other = _pc.Program("Other", "Other.exe");
        Set(_pc.User, Run, "Tool", $"\"{tool}\" --tray");
        Set(_pc.User, Run, "Other", other);
        Set(_pc.User, Run, "Never switched", other);
        Set(_pc.User, StartupApproval.Run, "Tool", On);
        Set(_pc.User, StartupApproval.Run, "Other", Off);

        var list = _pc.Reader().Read();

        Assert.Empty(list.Unreadable);
        Assert.Equal(3, list.Entries.Count);
        var entry = Only("run/user/Tool");
        Assert.True(entry.Enabled);
        Assert.Equal("Tool.exe", entry.Name); // no description in the file: Windows shows its file name
        Assert.Equal(tool, entry.ExecutablePath);
        Assert.Equal(Path.GetDirectoryName(tool), entry.ProgramFolder);
        Assert.Equal(StartupSwitch.Direct, entry.Switch);
        Assert.Null(entry.Problem);
        Assert.False(Only("run/user/Other").Enabled);
        Assert.True(Only("run/user/Never switched").Enabled);
    }

    [Fact]
    public void Only_non_empty_string_values_are_entries_and_a_flag_without_a_value_is_not_one()
    {
        var tool = _pc.Program("Vendor", "Tool.exe");
        Set(_pc.User, Run, "Number", 1);
        Set(_pc.User, Run, "Blank", "   ");
        Set(_pc.User, Run, "Expandable", "%NOT_A_VARIABLE%");
        using (var run = _pc.User.CreateSubKey(Run))
        {
            run.SetValue("Expanded", tool, RegistryValueKind.ExpandString);
        }
        Set(_pc.User, StartupApproval.Run, "Uninstalled long ago", Off);

        var ids = _pc.Reader().Read().Entries.Select(e => e.Id).Order();

        Assert.Equal(["run/user/Expandable", "run/user/Expanded"], ids);
        Assert.NotNull(Only("run/user/Expandable").Problem); // names a program that is not there
    }

    [Fact]
    public void Machine_entries_take_their_flags_from_the_machine_and_need_an_administrator()
    {
        var tool = _pc.Program("Vendor", "Tool.exe");
        Set(_pc.Machine, Run, "Tool", tool);
        Set(_pc.Machine, Run32, "Tool", tool);
        Set(_pc.Machine, StartupApproval.Run32, "Tool", Off);
        Set(_pc.User, StartupApproval.Run, "Tool", Off); // a user flag does not switch a machine entry

        var native = Only("run/machine/Tool");
        var wow = Only("run/machine32/Tool");

        Assert.True(native.Enabled);
        Assert.False(wow.Enabled);
        Assert.All([native, wow], e => Assert.Equal(StartupSwitch.NeedsAdmin, e.Switch));
        Assert.StartsWith(@"HKLM\", wow.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_folder_files_are_listed_by_what_they_start()
    {
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        Link(Path.Combine(_pc.UserFolder, "Console.lnk"), cmd, "/k echo hi");
        Link(Path.Combine(_pc.UserFolder, "Gone.lnk"), Path.Combine(_pc.Root, "deleted.exe"), "");
        File.WriteAllText(Path.Combine(_pc.UserFolder, "start.bat"), "@echo off");
        var ini = Path.Combine(_pc.UserFolder, "desktop.ini");
        File.WriteAllText(ini, "[.ShellClassInfo]");
        File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System);
        Set(_pc.User, StartupApproval.Folder, "start.bat", Off);
        Link(Path.Combine(_pc.MachineFolder, "Everyone.lnk"), cmd, "");

        var list = _pc.Reader().Read();

        Assert.Equal(4, list.Entries.Count); // desktop.ini is the folder's own
        var console = Only("folder/user/Console.lnk");
        Assert.Equal(ShellProperties.Read(cmd).Description ?? "cmd.exe", console.Name);
        Assert.Equal(cmd, console.ExecutablePath, ignoreCase: true);
        Assert.Contains("/k echo hi", console.Command, StringComparison.Ordinal);
        Assert.True(console.Enabled);
        Assert.NotNull(Only("folder/user/Gone.lnk").Problem);
        var bat = Only("folder/user/start.bat");
        Assert.False(bat.Enabled);
        Assert.Equal(bat.Command, bat.ExecutablePath);
        Assert.Equal(StartupSwitch.NeedsAdmin, Only("folder/machine/Everyone.lnk").Switch);
    }

    [Fact]
    public void A_program_that_runs_other_code_is_not_credited_with_processes()
    {
        Set(_pc.User, Run, "Script", @"cmd.exe /c start C:\tools\x.bat");
        File.WriteAllText(Path.Combine(_pc.UserFolder, "junk.lnk"), "not a shortcut");

        var script = Only("run/user/Script");

        Assert.False(script.CountsProcesses);
        Assert.Null(script.ProgramFolder);
        Assert.Null(script.Problem);
        Assert.Equal("The shortcut cannot be read.", Only("folder/user/junk.lnk").Problem);
    }

    [Fact]
    public void Missing_keys_and_folders_are_an_empty_list_not_an_error()
    {
        Directory.Delete(_pc.UserFolder);
        Directory.Delete(_pc.MachineFolder);

        var list = _pc.Reader().Read();

        Assert.Empty(list.Entries);
        Assert.Empty(list.Unreadable);
    }

    [Fact]
    public void A_store_apps_task_is_read_from_its_newest_manifest_with_the_users_state()
    {
        const string Task = """
            <uap5:Extension Category="windows.startupTask" Executable="app\Fake.exe">
              <uap5:StartupTask TaskId="FakeTask" Enabled="true" DisplayName="Fake at sign-in" />
            </uap5:Extension>
            """;
        var root = _pc.Package("Fake.App_1.2.0.0_x64__pub123", Manifest(Task), displayName: "Fake App");
        _pc.Package("Fake.App_1.0.0.0_x64__pub123", Manifest(Task.Replace("FakeTask", "OldTask", StringComparison.Ordinal)));
        _pc.Package("Plain.App_1.0.0.0_x64__pub123", Manifest("")); // declares no startup task
        Directory.CreateDirectory(Path.Combine(root, "app"));
        File.WriteAllText(Path.Combine(root, "app", "Fake.exe"), "not a real program");
        Set(_pc.User, $@"{TaskStates}\Fake.App_pub123\FakeTask", "State", 1); // DisabledByUser

        var entry = Assert.Single(_pc.Reader().Read().Entries);

        Assert.Equal("task/Fake.App_pub123/FakeTask", entry.Id);
        Assert.Equal("Fake at sign-in", entry.Name);
        Assert.Equal("Fake Publisher", entry.Publisher);
        Assert.Equal(Path.Combine(root, "app", "Fake.exe"), entry.ExecutablePath);
        Assert.Equal(root, entry.ProgramFolder);
        Assert.False(entry.Enabled);
        Assert.Equal(StartupSwitch.OsSettings, entry.Switch);
        Assert.Null(entry.Problem);
    }

    [Fact]
    public void Resource_packages_and_repeated_task_ids_add_no_entries()
    {
        const string Task = """
            <uap5:Extension Category="windows.startupTask" Executable="app\Fake.exe">
              <uap5:StartupTask TaskId="FakeTask" Enabled="true" DisplayName="Fake at sign-in" />
            </uap5:Extension>
            """;
        _pc.Package("Fake.App_1.0.0.0_x64__pub123", Manifest(Task + Task));
        _pc.Package("Fake.App_2.0.0.0_neutral_split.scale-125_pub123", Manifest("")); // newer, but a resource package

        var entry = Assert.Single(_pc.Reader().Read().Entries);

        Assert.Equal("task/Fake.App_pub123/FakeTask", entry.Id);
    }

    [Fact]
    public void A_broken_manifest_is_reported_and_the_rest_still_listed()
    {
        _pc.Package("Broken.App_1.0.0.0_x64__pub123", "<Package> windows.startupTask <unclosed");
        var tool = _pc.Program("Vendor", "Tool.exe");
        Set(_pc.User, Run, "Tool", tool);

        var list = _pc.Reader().Read();

        Assert.Equal("run/user/Tool", Assert.Single(list.Entries).Id);
        Assert.Contains("Broken.App", Assert.Single(list.Unreadable), StringComparison.Ordinal);
    }

    [Fact]
    public void A_program_owns_its_folder_unless_the_folder_is_shared()
    {
        var reader = _pc.Reader();

        Assert.Equal(Path.Combine(_pc.ProgramFiles, "Vendor"), reader.ProgramFolder(Path.Combine(_pc.ProgramFiles, "Vendor", "app.exe")));
        Assert.Null(reader.ProgramFolder(Path.Combine(_pc.ProgramFiles, "app.exe")));
        Assert.Null(reader.ProgramFolder(Path.Combine(_pc.WindowsFolder, "System32", "DriverStore", "x.exe")));
        Assert.Null(reader.ProgramFolder(Path.Combine(_pc.Root, "x.exe")));
        Assert.Null(reader.ProgramFolder(Path.Combine(_pc.UserFolder, "x.bat")));
        Assert.Null(reader.ProgramFolder(@"C:\x.exe")); // the drive root holds every shared folder
        Assert.Null(reader.ProgramFolder(null));
    }
}

[SupportedOSPlatform("windows")]
public class StartupRulesTests
{
    [Theory]
    [InlineData(null, true)] // never switched
    [InlineData(new byte[0], true)]
    [InlineData(new byte[] { 2, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 6, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 3, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 7, 0, 0, 0 }, false)] // Windows Security's icon, Off in Settings
    [InlineData(new byte[] { 1, 0, 0, 0 }, false)]
    public void A_flag_with_an_odd_first_byte_is_off(byte[]? flag, bool enabled) =>
        Assert.Equal(enabled, StartupApproval.IsEnabled(flag));

    [Fact]
    public void A_flag_of_another_type_is_ignored() => Assert.True(StartupApproval.IsEnabled(3));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void A_store_task_state_is_on_when_enabled_by_the_user_or_policy(int state, bool enabled)
    {
        Assert.Equal(enabled, StoreStartupTasks.IsEnabled(state, declaredOn: !enabled));
    }

    [Fact]
    public void A_store_task_without_a_state_takes_its_declared_default()
    {
        Assert.True(StoreStartupTasks.IsEnabled(null, declaredOn: true));
        Assert.False(StoreStartupTasks.IsEnabled(null, declaredOn: false));
    }

    private static readonly HashSet<string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\Program Files\A B\x.exe",
        @"C:\Tools\run",
        @"C:\Tools\run.exe",
        @"D:\Apps\app.exe",
    };

    [Theory]
    [InlineData("\"C:\\Program Files\\A B\\x.exe\" -s", @"C:\Program Files\A B\x.exe")]
    [InlineData("C:\\Program Files\\A B\\x.exe -s", @"C:\Program Files\A B\x.exe")] // unquoted spaces
    [InlineData("C:\\Tools\\run -x", @"C:\Tools\run")] // the file as named comes before name.exe
    [InlineData("C:\\Missing\\gone.exe /tray", @"C:\Missing\gone.exe")] // missing: still named
    [InlineData("C:\\Missing\\gone /tray", @"C:\Missing\gone")]
    [InlineData("\"D:\\Apps\\app.exe", @"D:\Apps\app.exe")] // unclosed quote
    [InlineData("   ", null)]
    [InlineData("\"\"", null)]
    public void A_command_names_the_program_windows_would_start(string command, string? expected) =>
        Assert.Equal(expected, CommandLine.Executable(command, Files.Contains));

    [Fact]
    public void Environment_variables_and_bare_names_are_resolved_where_windows_looks()
    {
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        Assert.Equal(cmd, CommandLine.Executable("cmd /c exit"), ignoreCase: true);
        Assert.Equal(cmd, CommandLine.Executable(@"%SystemRoot%\System32\cmd.exe /c exit"), ignoreCase: true);
        Assert.Equal("no-such-program.exe", CommandLine.Executable("no-such-program --flag"));
        var drive = Path.GetPathRoot(Environment.SystemDirectory)!;
        Assert.Equal(Path.Combine(drive, @"tools\x.exe"), CommandLine.Executable(@"\tools\x.exe -a"), ignoreCase: true);
    }
}
