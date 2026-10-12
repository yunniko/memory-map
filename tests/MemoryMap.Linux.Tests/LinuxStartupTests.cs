using System.Runtime.Versioning;
using MemoryMap.Core;
using MemoryMap.Linux.Startup;

namespace MemoryMap.Linux.Tests;

/// <summary>
/// The autostart reader and switch against throwaway folders standing in for ~/.config/autostart,
/// /etc/xdg/autostart and a PATH folder; the real ones are never touched (G-003 constraints).
/// Linux only: they resolve paths and check execute bits through libc.
/// </summary>
public sealed class LinuxStartupTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("memorymap-startup-").FullName;
    private readonly List<string> _shown = [];

    public LinuxStartupTests()
    {
        Locations = new LinuxStartupLocations(Path.Combine(_root, "config"), [Path.Combine(_root, "xdg")], ["GNOME"],
            [Path.Combine(_root, "bin")], Path.Combine(_root, "home"));
        Directory.CreateDirectory(Locations.UserFolder);
        Directory.CreateDirectory(SystemFolder);
        Directory.CreateDirectory(Path.Combine(_root, "bin"));
        Directory.CreateDirectory(Path.Combine(_root, "apps", "tool"));
        if (OperatingSystem.IsLinux())
        {
            Program(Path.Combine(_root, "apps", "tool", "tool"), "\x7fELF");
            File.CreateSymbolicLink(Path.Combine(_root, "bin", "tool"), Path.Combine(_root, "apps", "tool", "tool"));
            Program(Path.Combine(_root, "bin", "plain"), "\x7fELF");
            Program(Path.Combine(_root, "bin", "bash"), "\x7fELF");
            Program(Path.Combine(_root, "bin", "lua5.4"), "\x7fELF");
            Program(Path.Combine(_root, "bin", "script.sh"), "#!/bin/sh\n");
            Program(Path.Combine(_root, "apps", "tool", "run.sh"), "#!/bin/sh\n");
        }
    }

    private LinuxStartupLocations Locations { get; }

    private string SystemFolder => Locations.SystemFolders.Single();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [UnsupportedOSPlatform("windows")]
    private static void Program(string path, string content)
    {
        File.WriteAllText(path, content);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private string User(string name, string text) => Write(Locations.UserFolder, name, text);

    private string System(string name, string text) => Write(SystemFolder, name, text);

    private static string Write(string folder, string name, string text)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static string Entry(string name, string exec, string extra = "") =>
        $"[Desktop Entry]\nType=Application\nName={name}\nExec={exec}\n{extra}";

    private StartupList Read(LinuxStartupLocations? locations = null) => new LinuxStartupReader(locations ?? Locations, _ => null).Read();

    private StartupEntry Only(string name) => Read().Entries.Single(e => e.Id == "xdg/" + name);

    private LinuxStartupActions Actions() => new(Locations, path =>
    {
        _shown.Add(path);
        return null;
    });

    [Fact]
    public void User_and_system_entries_are_listed_with_where_they_live()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        User("mine.desktop", Entry("Mine", "plain --x"));
        System("shared.desktop", Entry("Shared", "plain"));
        System("copied.desktop", Entry("Copied", "plain"));
        User("copied.desktop", Entry("Copied", "plain"));

        var entries = Read().Entries;

        Assert.Equal(["Copied", "Mine", "Shared"], entries.Select(e => e.Name));
        Assert.Equal((StartupScope.User, Locations.UserFolder), (entries[1].Scope, entries[1].Location));
        Assert.Equal((StartupScope.Machine, $"{SystemFolder} (every user)"), (entries[2].Scope, entries[2].Location));
        Assert.Equal($"{SystemFolder} (every user), your copy in {Locations.UserFolder}", entries[0].Location);
        Assert.All(entries, e => Assert.Equal((StartupSource.XdgAutostart, StartupSwitch.Direct, true), (e.Source, e.Switch, e.Enabled)));
    }

    [Fact]
    public void The_users_copy_decides_whether_a_system_entry_starts()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        System("a.desktop", Entry("A", "plain"));
        User("a.desktop", "[Desktop Entry]\nHidden=true\n"); // a bare override, as some desktops write
        System("b.desktop", Entry("B", "plain", "X-GNOME-Autostart-enabled=false\n"));

        var a = Only("a.desktop");

        Assert.Equal(("A", "plain", false), (a.Name, a.Command, a.Enabled)); // described by the system file
        Assert.False(Only("b.desktop").Enabled);
    }

    [Fact]
    public void Entries_the_session_would_not_start_on_this_desktop_are_left_out()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        System("kde.desktop", Entry("Kde", "plain", "OnlyShowIn=KDE;\n"));
        System("gnome.desktop", Entry("Gnome", "plain", "OnlyShowIn=KDE;GNOME;\n"));
        System("not-gnome.desktop", Entry("NotGnome", "plain", "NotShowIn=GNOME;\n"));
        System("link.desktop", "[Desktop Entry]\nType=Link\nName=Link\nURL=https://example.org\n");

        Assert.Equal(["Gnome"], Read().Entries.Select(e => e.Name));
    }

    [Fact]
    public void Programs_are_resolved_through_path_and_links_to_the_file_their_processes_report()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        User("tool.desktop", Entry("Tool", "tool %U"));
        User("plain.desktop", Entry("Plain", "plain"));

        var tool = Only("tool.desktop");
        var plain = Only("plain.desktop");

        var real = Libc.RealPath(Path.Combine(_root, "apps", "tool", "tool"));
        Assert.Equal((real, Path.GetDirectoryName(real), true), (tool.ExecutablePath, tool.ProgramFolder, tool.CountsProcesses));
        Assert.Null(tool.Problem);
        // On the PATH, a folder many programs share: matched by its file only.
        Assert.Equal((Libc.RealPath(Path.Combine(_root, "bin", "plain")), null, true), (plain.ExecutablePath, plain.ProgramFolder, plain.CountsProcesses));
    }

    [Fact]
    public void Hosts_and_scripts_without_a_folder_of_their_own_are_not_counted()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        User("shell.desktop", Entry("Shell", "bash -c \"plain\""));
        User("script.desktop", Entry("Script", "script.sh"));
        User("lua.desktop", Entry("Lua", "lua5.4 game.lua"));
        User("own.desktop", Entry("Own", Path.Combine(_root, "apps", "tool", "run.sh")));

        Assert.False(Only("shell.desktop").CountsProcesses);
        Assert.False(Only("script.desktop").CountsProcesses);
        Assert.False(Only("lua.desktop").CountsProcesses); // a versioned host
        Assert.True(Only("own.desktop").CountsProcesses); // its folder tells its processes apart
    }

    [Fact]
    public void An_entry_that_will_not_start_says_why()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        User("missing.desktop", Entry("Missing", "nowhere-to-be-found"));
        User("gone.desktop", Entry("Gone", Path.Combine(_root, "apps", "gone")));
        User("try.desktop", Entry("Try", "plain", "TryExec=not-installed\n"));
        User("quote.desktop", Entry("Quote", "\"plain"));
        User("empty.desktop", "[Desktop Entry]\nType=Application\nName=Empty\n");

        Assert.Equal("The program is not installed: nowhere-to-be-found is not on the PATH.", Only("missing.desktop").Problem);
        Assert.StartsWith("The program file is missing: ", Only("gone.desktop").Problem);
        Assert.Equal("It will not start: not-installed is not installed (TryExec).", Only("try.desktop").Problem);
        Assert.Equal("The command cannot be read: it leaves a quote open.", Only("quote.desktop").Problem);
        Assert.Equal("The entry has no command to run.", Only("empty.desktop").Problem);
    }

    [Fact]
    public void An_unreadable_file_is_reported_and_the_rest_still_listed()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        User("broken.desktop", "Name=No group\n");
        User("fine.desktop", Entry("Fine", "plain"));

        var list = Read();

        Assert.Equal(["Fine"], list.Entries.Select(e => e.Name));
        Assert.Contains(list.Unreadable, u => u.Contains("broken.desktop", StringComparison.Ordinal));
    }

    [Fact]
    public void Systemd_user_services_are_listed_read_only_after_the_autostart_entries()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        var show = SystemdUserServicesTests.Show.Replace("/home/user", Locations.Home, StringComparison.Ordinal);
        var reader = new LinuxStartupReader(Locations, args => new CommandResult(0,
            args.Contains("show") ? show : SystemdUserServicesTests.Listing, ""));

        var services = reader.Read().Entries;

        Assert.Equal(["PipeWire Multimedia Service", "Session Migration"], services.Select(e => e.Name));
        Assert.All(services, s => Assert.Equal((StartupSource.SystemdUserService, StartupSwitch.ReadOnly), (s.Source, s.Switch)));
        Assert.Equal(StartupScope.User, services[0].Scope); // its unit file is in the home folder
        Assert.Equal(StartupScope.Machine, services[1].Scope);
        Assert.Equal(ActionOutcome.Refused, Actions().SetEnabled(services[0], false).Outcome);
    }

    [Fact]
    public void Switching_off_an_entry_for_every_user_writes_a_marked_copy_and_leaves_the_system_file_alone()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        var system = System("shared.desktop", Entry("Shared", "plain", "Comment=Kept\n"));
        var before = File.ReadAllBytes(system);

        var result = Actions().SetEnabled(Only("shared.desktop"), false);

        Assert.Equal(ActionOutcome.Done, result.Outcome);
        Assert.Equal(before, File.ReadAllBytes(system));
        var text = File.ReadAllText(Path.Combine(Locations.UserFolder, "shared.desktop"));
        var copy = DesktopEntry.Parse(text)!;
        Assert.Equal(("true", "Kept"), (copy["Hidden"], copy["Comment"]));
        Assert.True(LinuxStartupActions.IsUntouchedOwnCopy(text));
        Assert.False(Only("shared.desktop").Enabled);
        Assert.Empty(Directory.EnumerateFiles(Locations.UserFolder, "*.memory-map-tmp"));
    }

    [Fact]
    public void Switching_it_back_on_removes_only_the_apps_own_copy()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        var system = System("shared.desktop", Entry("Shared", "plain"));
        var actions = Actions();
        actions.SetEnabled(Only("shared.desktop"), false);

        var result = actions.SetEnabled(Only("shared.desktop"), true);

        Assert.Equal(ActionOutcome.Done, result.Outcome);
        Assert.False(File.Exists(Path.Combine(Locations.UserFolder, "shared.desktop")));
        Assert.True(File.Exists(system));
        Assert.True(Only("shared.desktop").Enabled);
    }

    [Fact]
    public void The_apps_own_copy_edited_since_is_the_users_and_is_kept()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        System("shared.desktop", Entry("Shared", "plain"));
        var actions = Actions();
        actions.SetEnabled(Only("shared.desktop"), false);
        var copy = Path.Combine(Locations.UserFolder, "shared.desktop");
        File.WriteAllText(copy, File.ReadAllText(copy).Replace("Exec=plain", "Exec=plain --mine", StringComparison.Ordinal));

        Assert.Equal(ActionOutcome.Done, actions.SetEnabled(Only("shared.desktop"), true).Outcome);
        Assert.Contains("Exec=plain --mine\n", File.ReadAllText(copy), StringComparison.Ordinal);
        Assert.True(Only("shared.desktop").Enabled);
    }

    [Fact]
    public void The_apps_own_copy_switched_again_stays_its_own()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        // Off in the system file: switching it on needs a copy, and off again keeps that copy the app's.
        System("shared.desktop", Entry("Shared", "plain", "Hidden=true\n"));
        var actions = Actions();
        var copy = Path.Combine(Locations.UserFolder, "shared.desktop");

        actions.SetEnabled(Only("shared.desktop"), true);
        actions.SetEnabled(Only("shared.desktop"), false);

        Assert.True(LinuxStartupActions.IsUntouchedOwnCopy(File.ReadAllText(copy)));
        Assert.False(Only("shared.desktop").Enabled);
    }

    [Fact]
    public void A_copy_the_user_wrote_is_switched_in_place_and_never_removed()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        System("shared.desktop", Entry("Shared", "plain"));
        var copy = User("shared.desktop", Entry("Shared", "plain --mine", "Hidden=true\n"));

        var result = Actions().SetEnabled(Only("shared.desktop"), true);

        Assert.Equal(ActionOutcome.Done, result.Outcome);
        var text = File.ReadAllText(copy);
        Assert.Contains("Exec=plain --mine\n", text, StringComparison.Ordinal);
        Assert.Contains("Hidden=false\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain(LinuxStartupActions.OverrideKey, text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_bare_override_is_switched_on_from_the_system_entry()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        System("shared.desktop", Entry("Shared", "plain --system"));
        var copy = User("shared.desktop", "[Desktop Entry]\n# my note\nHidden=true\nX-Mine=1\n");

        Actions().SetEnabled(Only("shared.desktop"), true);

        var text = File.ReadAllText(copy);
        var entry = DesktopEntry.Parse(text)!;
        Assert.Equal(("plain --system", "false", "1"), (entry["Exec"], entry["Hidden"], entry["X-Mine"]));
        Assert.Contains("# my note\n", text, StringComparison.Ordinal);
        Assert.True(Only("shared.desktop").Enabled);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void A_linked_entry_is_written_through_the_link_and_keeps_its_permissions()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        Directory.CreateDirectory(Path.Combine(_root, "dotfiles"));
        var target = Write(Path.Combine(_root, "dotfiles"), "mine.desktop", Entry("Mine", "plain"));
        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var link = Path.Combine(Locations.UserFolder, "mine.desktop");
        File.CreateSymbolicLink(link, target);

        Assert.Equal(ActionOutcome.Done, Actions().SetEnabled(Only("mine.desktop"), false).Outcome);

        Assert.Equal(target, new FileInfo(link).LinkTarget);
        Assert.Contains("Hidden=true\n", File.ReadAllText(target), StringComparison.Ordinal);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(target));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "dotfiles"), "*.memory-map-tmp"));
    }

    [Fact]
    public void A_users_own_entry_is_toggled_in_place_keeping_its_other_lines()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        var path = User("mine.desktop", Entry("Mine", "plain", "Comment=Kept\nX-GNOME-Autostart-enabled=false\n"));
        var actions = Actions();

        Assert.Equal(ActionOutcome.Done, actions.SetEnabled(Only("mine.desktop"), true).Outcome);
        Assert.Equal(Entry("Mine", "plain", "Comment=Kept\nX-GNOME-Autostart-enabled=true\nHidden=false\n"), File.ReadAllText(path));

        Assert.Equal(ActionOutcome.Done, actions.SetEnabled(Only("mine.desktop"), false).Outcome);
        Assert.True(File.Exists(path)); // never removed: the user wrote it
        Assert.False(Only("mine.desktop").Enabled);
    }

    [Fact]
    public void An_entry_removed_meanwhile_is_reported_gone()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        var path = User("mine.desktop", Entry("Mine", "plain"));
        var entry = Only("mine.desktop");
        File.Delete(path);

        Assert.Equal(ActionOutcome.AlreadyExited, Actions().SetEnabled(entry, false).Outcome);
        Assert.Empty(Directory.EnumerateFiles(Locations.UserFolder));
    }

    [Fact]
    public void Open_file_location_shows_the_program_or_else_the_entrys_file()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Resolves paths through libc.");
        User("plain.desktop", Entry("Plain", "plain"));
        var missing = User("missing.desktop", Entry("Missing", "nowhere-to-be-found"));
        var actions = Actions();

        actions.ShowFileLocation(Only("plain.desktop"));
        actions.ShowFileLocation(Only("missing.desktop"));

        Assert.Equal([Libc.RealPath(Path.Combine(_root, "bin", "plain"))!, missing], _shown);
    }
}
