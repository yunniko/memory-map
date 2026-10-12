namespace MemoryMap.Linux.Startup;

/// <summary>
/// Where the Linux reader looks, from the XDG Base Directory variables. <see cref="ForCurrentUser"/>
/// is the real session; tests point the same reader at temp folders, so they never touch real
/// entries (G-003 constraints).
/// </summary>
/// <param name="ConfigHome">$XDG_CONFIG_HOME (~/.config): its autostart folder is the user's, and
/// takes precedence over every system folder.</param>
/// <param name="ConfigDirs">$XDG_CONFIG_DIRS (/etc/xdg), most important first.</param>
/// <param name="Desktops">$XDG_CURRENT_DESKTOP's names, for OnlyShowIn and NotShowIn.</param>
/// <param name="SearchPath">$PATH, where a bare program name in Exec is looked up.</param>
/// <param name="Home">Shown as ~ in locations.</param>
public sealed record LinuxStartupLocations(
    string ConfigHome,
    IReadOnlyList<string> ConfigDirs,
    IReadOnlyList<string> Desktops,
    IReadOnlyList<string> SearchPath,
    string Home)
{
    public static LinuxStartupLocations ForCurrentUser()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        // The spec ignores a relative value, as if the variable were unset.
        static IReadOnlyList<string> Paths(string variable) =>
            (Environment.GetEnvironmentVariable(variable) ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries)
                .Where(Path.IsPathRooted).ToList();
        // One path, not a list: a colon is part of it.
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { } set && Path.IsPathRooted(set)
            ? set : Path.Combine(home, ".config");
        var configDirs = Paths("XDG_CONFIG_DIRS") is { Count: > 0 } dirs ? dirs : ["/etc/xdg"];
        var desktops = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries);
        return new(configHome, configDirs, desktops, Paths("PATH"), home);
    }

    public string UserFolder => Path.Combine(ConfigHome, "autostart");

    public IEnumerable<string> SystemFolders => ConfigDirs.Select(d => Path.Combine(d, "autostart"));

    /// <summary>A path as the user knows it: the home folder as ~.</summary>
    public string Display(string path) =>
        Home.Length > 1 && (path == Home || path.StartsWith(Home + "/", StringComparison.Ordinal)) ? "~" + path[Home.Length..] : path;

    /// <summary>Folders holding many programs' files: a program directly in one is matched by its
    /// file only, as on Windows. Resolved, as /proc reports paths.</summary>
    internal IReadOnlySet<string> SharedFolders()
    {
        string[] standard =
        [
            "/", "/bin", "/sbin", "/usr", "/usr/bin", "/usr/sbin", "/usr/local", "/usr/local/bin", "/usr/local/sbin",
            "/usr/lib", "/usr/lib32", "/usr/lib64", "/usr/libexec", "/usr/lib/x86_64-linux-gnu",
            "/usr/lib/x86_64-linux-gnu/libexec", "/usr/games", "/usr/local/lib", "/usr/local/libexec", "/usr/local/games",
            "/usr/share", "/opt", "/snap/bin", "/tmp", "/var/tmp", Home, Path.Combine(Home, ".local/bin"), Path.Combine(Home, "bin"),
            Path.Combine(Home, "Downloads"), Path.Combine(Home, "Desktop"), Path.Combine(Home, "Applications"),
        ];
        return standard.Concat(SearchPath).Append(UserFolder).Concat(SystemFolders)
            .Select(f => Libc.RealPath(f) ?? f.TrimEnd('/')).Select(f => f.Length == 0 ? "/" : f)
            .ToHashSet(StringComparer.Ordinal);
    }
}
