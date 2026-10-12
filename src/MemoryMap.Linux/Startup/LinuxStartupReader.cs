using System.Globalization;
using MemoryMap.Core;

namespace MemoryMap.Linux.Startup;

/// <summary>An autostart entry's files: the user's, and the first system folder's of the same name.</summary>
internal sealed record AutostartFiles(string Name, string? User, string? System)
{
    /// <summary>The file the session reads: the user's takes precedence (XDG Autostart spec).</summary>
    public string? Effective => User ?? System;
}

/// <summary>
/// Every program a Linux desktop session starts at sign-in: the XDG autostart folders (the
/// user's, then $XDG_CONFIG_DIRS'), as the session reads them, and the enabled systemd user
/// services. It only reads. See D032 and D033.
/// </summary>
public sealed class LinuxStartupReader : IStartupReader
{
    internal const string AutostartPrefix = "xdg/";
    private const string ServicePrefix = "systemd/";

    private readonly LinuxStartupLocations _locations;
    private readonly Func<string[], CommandResult?> _systemctl;
    private IReadOnlySet<string>? _shared;

    public LinuxStartupReader()
        : this(LinuxStartupLocations.ForCurrentUser())
    {
    }

    internal LinuxStartupReader(LinuxStartupLocations locations, Func<string[], CommandResult?>? systemctl = null)
    {
        _locations = locations;
        _systemctl = systemctl ?? (args => Command.Run("systemctl", args));
    }

    /// <inheritdoc/>
    public StartupList Read()
    {
        var entries = new List<StartupEntry>();
        var unreadable = new List<string>();
        foreach (var files in AllAutostartFiles(unreadable))
        {
            try
            {
                if (Autostart(files) is { } entry)
                {
                    entries.Add(entry);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                unreadable.Add($"{_locations.Display(files.Effective!)}: {e.Message}");
            }
        }
        try
        {
            entries.AddRange(SystemdUserServices.Read(_systemctl).Select(Service));
        }
        catch (IOException e)
        {
            unreadable.Add($"systemd user services: {e.Message}");
        }
        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase) is var c and not 0
            ? c : string.CompareOrdinal(a.Id, b.Id));
        return new StartupList(entries, unreadable);
    }

    // Resolved once per reader: realpath on some 25 folders, which do not move while it lives.
    private IReadOnlySet<string> Shared => _shared ??= _locations.SharedFolders();

    /// <summary>One autostart entry read again (to confirm a switch held); null if it is no longer listed.</summary>
    internal StartupEntry? ReadAutostart(string fileName) => Autostart(Files(fileName));

    /// <summary>The user's and the system's file named <paramref name="fileName"/>, where they exist.</summary>
    internal AutostartFiles Files(string fileName)
    {
        var user = Path.Combine(_locations.UserFolder, fileName);
        var system = _locations.SystemFolders.Select(f => Path.Combine(f, fileName)).FirstOrDefault(File.Exists);
        return new AutostartFiles(fileName, File.Exists(user) ? user : null, system);
    }

    private IEnumerable<AutostartFiles> AllAutostartFiles(List<string> unreadable)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var folder in _locations.SystemFolders.Prepend(_locations.UserFolder))
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    names.UnionWith(Directory.EnumerateFiles(folder, "*.desktop").Select(f => Path.GetFileName(f)));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                unreadable.Add($"{_locations.Display(folder)}: {e.Message}");
            }
        }
        return names.Select(Files);
    }

    /// <summary>The entry the session sees in <paramref name="files"/>; null if it starts nothing here.</summary>
    internal StartupEntry? Autostart(AutostartFiles files)
    {
        if (files.Effective is not { } effective)
        {
            return null;
        }
        var entry = Parse(effective);
        // A bare override (only Hidden=true, as some desktops write) describes nothing: the
        // system file says what the entry is, the user's whether it starts.
        var shown = files is { User: not null, System: not null } && entry["Exec"] is null ? Parse(files.System) : entry;
        if ((shown["Type"] is { } type && type != "Application") || !ForThisDesktop(shown))
        {
            return null; // the session would not start it here, so it is not in the list (D032)
        }
        var exec = shown["Exec"];
        var args = exec is null ? null : DesktopEntry.Arguments(exec);
        var program = ProgramFile.Of(args is [var first, ..] ? first : null, _locations.SearchPath, Shared);
        var problem = exec is not null && args is null ? "The command cannot be read: it leaves a quote open."
            : shown["TryExec"] is { } tryExec && !Installed(tryExec) ? $"It will not start: {tryExec} is not installed (TryExec)."
            : program.Problem;
        var name = shown.Localized("Name", CultureInfo.CurrentUICulture) is { Length: > 0 } n ? n : Path.GetFileNameWithoutExtension(files.Name);
        return new StartupEntry(AutostartPrefix + files.Name, name, null, exec ?? "", Location(files), StartupSource.XdgAutostart,
            files.System is null ? StartupScope.User : StartupScope.Machine, entry.StartsAtLogin, program.Path, program.Folder, problem, program.Counts);
    }

    private static DesktopEntry Parse(string path) =>
        DesktopEntry.Parse(File.ReadAllText(path)) ?? throw new InvalidDataException("not a desktop entry (no [Desktop Entry] group).");

    private bool ForThisDesktop(DesktopEntry entry)
    {
        bool Current(string desktop) => _locations.Desktops.Contains(desktop, StringComparer.OrdinalIgnoreCase);
        var only = entry.List("OnlyShowIn");
        return (only.Count == 0 || only.Any(Current)) && !entry.List("NotShowIn").Any(Current);
    }

    private bool Installed(string program) =>
        program.Contains('/') ? ProgramFile.IsExecutable(program) : ProgramFile.Find(program, _locations.SearchPath) is not null;

    private string Location(AutostartFiles files)
    {
        var user = _locations.Display(_locations.UserFolder);
        if (files.System is null)
        {
            return user;
        }
        var system = _locations.Display(Path.GetDirectoryName(files.System)!);
        return files.User is null ? $"{system} (every user)" : $"{system} (every user), your copy in {user}";
    }

    private StartupEntry Service(ServiceUnit unit)
    {
        var program = ProgramFile.Of(unit.Program, _locations.SearchPath, Shared);
        var mine = unit.FragmentPath is { } path && path.StartsWith(_locations.Home + "/", StringComparison.Ordinal);
        return new StartupEntry(ServicePrefix + unit.Id, unit.Description ?? unit.Id, null, unit.Arguments ?? unit.Program ?? "",
            $"systemd user service {unit.Id}", StartupSource.SystemdUserService, mine ? StartupScope.User : StartupScope.Machine,
            Enabled: true, program.Path, program.Folder, program.Problem, program.Counts);
    }
}
