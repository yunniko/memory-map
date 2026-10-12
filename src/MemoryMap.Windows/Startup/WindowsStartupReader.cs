using System.Runtime.Versioning;
using System.Security;
using MemoryMap.Core;
using Microsoft.Win32;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// Where the reader looks. <see cref="ForCurrentUser"/> is the real machine; tests point the
/// same reader at a scratch registry key and temp folders, so they never touch real entries.
/// </summary>
/// <param name="User">HKEY_CURRENT_USER, or a stand-in with the same layout below it.</param>
/// <param name="Machine">HKEY_LOCAL_MACHINE's 64-bit view, or a stand-in.</param>
/// <param name="WindowsFolder">Everything below it (System32, DriverStore) is shared by many programs.</param>
/// <param name="SharedFolders">Folders that hold many programs' files (Program Files itself, the
/// profile): a program directly in one of these is matched by its file only.</param>
[SupportedOSPlatform("windows")]
public sealed record StartupLocations(
    RegistryKey User,
    RegistryKey Machine,
    string UserFolder,
    string MachineFolder,
    string WindowsFolder,
    IReadOnlyList<string> SharedFolders)
{
    public static StartupLocations ForCurrentUser()
    {
        string Known(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
        var local = Known(Environment.SpecialFolder.LocalApplicationData);
        return new(
            Registry.CurrentUser,
            RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
            Known(Environment.SpecialFolder.Startup),
            Known(Environment.SpecialFolder.CommonStartup),
            Known(Environment.SpecialFolder.Windows),
            new[]
            {
                Known(Environment.SpecialFolder.ProgramFiles),
                Known(Environment.SpecialFolder.ProgramFilesX86),
                Known(Environment.SpecialFolder.CommonProgramFiles),
                Known(Environment.SpecialFolder.CommonProgramFilesX86),
                Known(Environment.SpecialFolder.CommonApplicationData),
                Known(Environment.SpecialFolder.UserProfile),
                Known(Environment.SpecialFolder.DesktopDirectory),
                Known(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Known(Environment.SpecialFolder.UserProfile), "Downloads"),
                Path.GetTempPath(),
                Known(Environment.SpecialFolder.ApplicationData),
                local,
                Path.Combine(local, "Programs"),
                Path.Combine(local, @"Microsoft\WindowsApps"),
                Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\",
            }.Where(f => f.Length > 0).ToList());
    }
}

/// <summary>
/// Every program Windows starts at sign-in, as Task Manager's Startup apps page lists them:
/// the Run keys (user, machine, machine 32-bit), both Startup folders and Store apps' startup
/// tasks. It only reads. See D018.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStartupReader(StartupLocations locations) : IStartupReader
{
    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string Run32Key = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;

    // Windows programs that run other code named in their arguments; their processes cannot be
    // told apart by file, so an entry starting one is not credited with any of them.
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd.exe", "conhost.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe", "mshta.exe",
        "rundll32.exe", "regsvr32.exe", "msiexec.exe", "explorer.exe", "wt.exe",
    };

    // Folders shared by many programs; the Windows folder counts with everything below it.
    private readonly string[] _sharedFolders =
        [.. locations.SharedFolders.Append(locations.WindowsFolder).Append(locations.UserFolder).Append(locations.MachineFolder)
            .Where(f => f.Length > 0)];

    public WindowsStartupReader()
        : this(StartupLocations.ForCurrentUser())
    {
    }

    /// <inheritdoc/>
    public StartupList Read()
    {
        var entries = new List<StartupEntry>();
        var unreadable = new List<string>();
        void Source(string what, Func<IEnumerable<StartupEntry>> read)
        {
            try
            {
                entries.AddRange(read());
            }
            catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException or ArgumentException or NotSupportedException)
            {
                unreadable.Add($"{what}: {e.Message}");
            }
        }

        Source("HKCU Run", () => RunValues(locations.User, RunKey, StartupApproval.Run, "user", StartupScope.User));
        Source("HKLM Run", () => RunValues(locations.Machine, RunKey, StartupApproval.Run, "machine", StartupScope.Machine));
        Source("HKLM Run (32-bit)", () => RunValues(locations.Machine, Run32Key, StartupApproval.Run32, "machine32", StartupScope.Machine));
        Source("Startup folder", () => FolderFiles(locations.UserFolder, locations.User, "user", StartupScope.User));
        Source("All users' Startup folder", () => FolderFiles(locations.MachineFolder, locations.Machine, "machine", StartupScope.Machine));
        Source("Store apps", () => StoreStartupTasks.Read(locations.User, unreadable));

        // A source can name the same entry twice (a manifest repeating a task id); Windows runs it once.
        var unique = entries.DistinctBy(e => e.Id, StringComparer.Ordinal).ToList();
        unique.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase) is var c and not 0
            ? c : string.CompareOrdinal(a.Id, b.Id));
        return new StartupList(unique, unreadable);
    }

    /// <summary>The problem text for a program file that is not there (shared with Store tasks).</summary>
    internal static string Missing(string exe) => $"The program file is missing: {exe}";

    private List<StartupEntry> RunValues(RegistryKey root, string keyPath, string approvalPath, string idScope, StartupScope scope)
    {
        using var key = root.OpenSubKey(keyPath);
        if (key is null)
        {
            return [];
        }
        using var approval = root.OpenSubKey(approvalPath);
        var location = $@"{(scope == StartupScope.User ? "HKCU" : "HKLM")}\{keyPath}";
        var entries = new List<StartupEntry>();
        foreach (var valueName in key.GetValueNames())
        {
            // Explorer runs string values only; any other kind is not an entry.
            if (key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string command
                || command.Trim().Length == 0)
            {
                continue;
            }
            var exe = CommandLine.Executable(command);
            entries.Add(Entry($"run/{idScope}/{valueName}", valueName, command, location, StartupSource.RunKey, scope,
                StartupApproval.IsEnabled(approval?.GetValue(valueName)), exe, exe is null ? "The command names no program." : null));
        }
        return entries;
    }

    private List<StartupEntry> FolderFiles(string folder, RegistryKey root, string idScope, StartupScope scope)
    {
        if (folder.Length == 0 || !Directory.Exists(folder))
        {
            return [];
        }
        using var approval = root.OpenSubKey(StartupApproval.Folder);
        var entries = new List<StartupEntry>();
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
        {
            // desktop.ini and other hidden or system files are the folder's own, not programs.
            if ((file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
            {
                continue;
            }
            var (command, exe, problem) = Launches(file);
            entries.Add(Entry($"folder/{idScope}/{file.Name}", Path.GetFileNameWithoutExtension(file.Name), command, folder,
                StartupSource.StartupFolder, scope, StartupApproval.IsEnabled(approval?.GetValue(file.Name)), exe, problem));
        }
        return entries;
    }

    // What a Startup-folder file starts: a shortcut's target; any other file (a program, script
    // or document) is opened as itself.
    private static (string Command, string? Exe, string? Problem) Launches(FileInfo file)
    {
        if (!file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return (file.FullName, file.FullName, null);
        }
        return Shortcut.Read(file.FullName) switch
        {
            null => (file.FullName, null, "The shortcut cannot be read."),
            // A shortcut to a Store app, a URL or another Shell item has no file target; Windows still opens it.
            { Target.Length: 0 } => (file.FullName, null, null),
            var (target, arguments) => (arguments.Length > 0 ? $"\"{target}\" {arguments}" : $"\"{target}\"", target, null),
        };
    }

    // A problem the caller found wins; otherwise one is reported when the named program is not
    // there (a folder target, which Windows opens in Explorer, is not a problem).
    private StartupEntry Entry(string id, string registeredName, string command, string location, StartupSource source,
        StartupScope scope, bool enabled, string? exe, string? problem = null)
    {
        var exists = exe is not null && File.Exists(exe);
        problem ??= exe is null || exists || Directory.Exists(exe) ? null : Missing(exe);
        var (description, company) = exists ? ShellProperties.Read(exe!) : (null, null);
        // Named as Task Manager and Settings name it: the file's description, else its file name.
        var name = description ?? (exists ? Path.GetFileName(exe!) : registeredName);
        var hosted = exe is not null && Hosts.Contains(Path.GetFileName(exe));
        return new StartupEntry(id, name, company, command, location, source, scope, enabled, exe,
            hosted ? null : ProgramFolder(exe), problem, CountsProcesses: !hosted);
    }

    /// <summary>The program's own folder, or null when that folder is shared by many programs.</summary>
    internal string? ProgramFolder(string? exe)
    {
        if (exe is null || Path.GetDirectoryName(exe) is not { Length: > 0 } folder
            || FolderPaths.IsUnder(folder, locations.WindowsFolder, PathComparison))
        {
            return null;
        }
        // The folder is shared when it is one of these, or holds one (C:\ holds them all).
        return _sharedFolders.Any(s => FolderPaths.Same(folder, s, PathComparison) || FolderPaths.IsUnder(s, folder, PathComparison))
            ? null : folder;
    }
}
