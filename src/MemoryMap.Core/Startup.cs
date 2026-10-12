namespace MemoryMap.Core;

/// <summary>Where an entry is registered, as the OS-specific reader found it.</summary>
public enum StartupSource
{
    /// <summary>A registry Run value.</summary>
    RunKey,

    /// <summary>A file in a Startup folder.</summary>
    StartupFolder,

    /// <summary>A Store (packaged) app's declared startup task.</summary>
    StoreTask,
}

/// <summary>Whose sign-in starts the entry: this user's only, or every user's.</summary>
public enum StartupScope
{
    User,
    Machine,
}

/// <summary>How the entry can be switched on or off.</summary>
public enum StartupSwitch
{
    /// <summary>Directly, as this user.</summary>
    Direct,

    /// <summary>Through an administrator prompt: it starts for every user.</summary>
    NeedsAdmin,

    /// <summary>Only in the OS's own settings: the app owns the switch.</summary>
    OsSettings,
}

/// <summary>
/// One program that starts at sign-in.
/// </summary>
/// <param name="Id">Stable across reads: source, scope and registered name. Never a display label.</param>
/// <param name="Name">What the OS's own startup list calls it.</param>
/// <param name="Publisher">The program's publisher; null when the file does not say.</param>
/// <param name="Command">The command as registered (for a shortcut: its target and arguments).</param>
/// <param name="Location">Where it is registered, worded for the user.</param>
/// <param name="ExecutablePath">The program file the command runs; null when it cannot be worked out.</param>
/// <param name="ProgramFolder">Processes whose file is in this folder count as this program
/// (a launcher often starts the real app and exits). Null when the folder is shared with other
/// programs, such as the Windows folder; then only <paramref name="ExecutablePath"/> counts.</param>
/// <param name="Problem">Why the entry will not start as registered (file missing, broken
/// shortcut); null when it looks fine.</param>
/// <param name="CountsProcesses">False when the program is a host that runs other code (cmd,
/// rundll32, a script host): its processes cannot be told apart by file, so none is counted.</param>
public sealed record StartupEntry(
    string Id,
    string Name,
    string? Publisher,
    string Command,
    string Location,
    StartupSource Source,
    StartupScope Scope,
    bool Enabled,
    string? ExecutablePath,
    string? ProgramFolder,
    string? Problem = null,
    bool CountsProcesses = true)
{
    public StartupSwitch Switch => Source == StartupSource.StoreTask ? StartupSwitch.OsSettings
        : Scope == StartupScope.Machine ? StartupSwitch.NeedsAdmin
        : StartupSwitch.Direct;
}

/// <summary>What one read found, sorted by name, and the sources it could not read, so nothing
/// is silently missing.</summary>
public sealed record StartupList(IReadOnlyList<StartupEntry> Entries, IReadOnlyList<string> Unreadable);

/// <summary>Reads every program that starts at sign-in. One implementation per OS.</summary>
public interface IStartupReader
{
    /// <summary>Never throws for one bad entry or source: an entry carries a
    /// <see cref="StartupEntry.Problem"/>, a source that cannot be read is listed as unreadable.</summary>
    StartupList Read();
}

/// <summary>Finds the file a running process was started from. One implementation per OS.</summary>
public interface IProcessPaths
{
    /// <summary>The process's executable path; null when the OS will not say (protected or exited).</summary>
    string? PathOf(ProcessSample process);

    /// <summary>How this OS's file system compares paths (Windows ignores case; Linux does not).</summary>
    StringComparison Comparison { get; }
}

/// <summary>Folder containment on either separator, with the caller's (the OS's) case rule.</summary>
public static class FolderPaths
{
    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>True when <paramref name="path"/> lies strictly below <paramref name="folder"/>.</summary>
    public static bool IsUnder(string path, string folder, StringComparison comparison)
    {
        var trimmed = folder.TrimEnd(Separators);
        return path.Length > trimmed.Length + 1
            && path.StartsWith(trimmed, comparison)
            && path[trimmed.Length] is '\\' or '/';
    }

    /// <summary>True when both name the same folder, ignoring a trailing separator.</summary>
    public static bool Same(string a, string b, StringComparison comparison) =>
        string.Equals(a.TrimEnd(Separators), b.TrimEnd(Separators), comparison);
}

/// <summary>What a startup entry is costing right now.</summary>
public sealed record StartupUsage(IReadOnlyList<ProcessSample> Processes)
{
    public static readonly StartupUsage None = new([]);

    public bool Running => Processes.Count > 0;

    /// <summary>The same measure as the map's tiles (D003).</summary>
    public long PrivateWorkingSet => Processes.Sum(p => p.PrivateWorkingSet);
}

public static class StartupMatching
{

    /// <summary>
    /// The running processes that belong to each entry: started from its program file, or from
    /// its program folder or below. A process belongs to at most one entry, the one with the
    /// most specific match (its exact file, else the deepest folder), so two entries in nested
    /// folders do not both count it. Ties go to the first entry in list order.
    /// </summary>
    public static IReadOnlyDictionary<string, StartupUsage> Match(
        IReadOnlyList<StartupEntry> entries, IEnumerable<ProcessSample> processes, IProcessPaths paths)
    {
        // Ids are unique in a reader's list; a repeat would share its first entry's result.
        var found = new Dictionary<string, List<ProcessSample>>();
        foreach (var entry in entries)
        {
            found.TryAdd(entry.Id, []);
        }
        foreach (var process in processes)
        {
            if (paths.PathOf(process) is not { } path)
            {
                continue;
            }
            StartupEntry? best = null;
            var bestScore = 0;
            foreach (var entry in entries)
            {
                if (!entry.CountsProcesses)
                {
                    continue;
                }
                var score = Score(entry, path, paths.Comparison);
                if (score > bestScore)
                {
                    (best, bestScore) = (entry, score);
                }
            }
            if (best is not null)
            {
                found[best.Id].Add(process);
            }
        }
        return found.ToDictionary(kv => kv.Key, kv => kv.Value.Count == 0 ? StartupUsage.None
            : new StartupUsage(kv.Value.OrderByDescending(p => p.PrivateWorkingSet).ToList()));
    }

    // 0 = no match; an exact file beats any folder; a deeper folder beats a shallower one.
    private static int Score(StartupEntry entry, string path, StringComparison comparison)
    {
        if (entry.ExecutablePath is { } exe && string.Equals(exe, path, comparison))
        {
            return int.MaxValue;
        }
        if (entry.ProgramFolder is { } folder && FolderPaths.IsUnder(path, folder, comparison))
        {
            return folder.Length;
        }
        return 0;
    }
}
