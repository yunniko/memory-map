namespace MemoryMap.Linux.Startup;

/// <summary>
/// The program a startup command runs, as its processes will report it: the resolved file, the
/// folder its processes may also run from, why it will not start, and whether its processes
/// can be told apart at all (D020, D034).
/// </summary>
internal sealed record ProgramFile(string? Path, string? Folder, string? Problem, bool Counts)
{
    // Programs that run other code named in their arguments, so their processes cannot be told
    // apart by file: shells, interpreters and launchers (Linux's cmd, rundll32 and script hosts).
    private static readonly HashSet<string> Hosts = new(StringComparer.Ordinal)
    {
        "sh", "bash", "dash", "zsh", "fish", "ksh", "csh", "tcsh", "busybox", "env", "nohup", "setsid", "systemd-run",
        "nice", "ionice", "taskset", "sudo", "pkexec", "runuser", "timeout", "stdbuf",
        "perl", "ruby", "node", "gjs", "php", "lua", "deno", "bun", "java", "mono", "dotnet", "wine", "wine64",
        "flatpak", "snap", "xdg-open", "gio", "dbus-launch", "dbus-run-session",
        "xterm", "gnome-terminal", "konsole", "xfce4-terminal", "x-terminal-emulator",
    };

    /// <param name="program">The command's first argument: a path, or a name looked up in PATH.</param>
    public static ProgramFile Of(string? program, IReadOnlyList<string> searchPath, IReadOnlySet<string> sharedFolders)
    {
        if (string.IsNullOrEmpty(program))
        {
            return new(null, null, "The entry has no command to run.", Counts: false);
        }
        var found = program.Contains('/') ? program : Find(program, searchPath);
        if (found is null)
        {
            return new(null, null, $"The program is not installed: {program} is not on the PATH.", Counts: false);
        }
        if (Libc.RealPath(found) is not { } real || !File.Exists(real))
        {
            return new(found, null, $"The program file is missing: {found}", Counts: false);
        }
        var name = System.IO.Path.GetFileName(real);
        // Versioned names too: python3.12, lua5.4, php8.3.
        if (Hosts.Contains(name) || Hosts.Contains(name.TrimEnd("0123456789.".ToCharArray()))
            || name.StartsWith("python", StringComparison.Ordinal))
        {
            return new(real, null, null, Counts: false);
        }
        var folder = System.IO.Path.GetDirectoryName(real);
        var dedicated = folder is not null && !sharedFolders.Contains(folder) ? folder : null;
        // A script runs as its interpreter: only a folder of its own tells its processes apart.
        return new(real, dedicated, null, Counts: dedicated is not null || !IsScript(real));
    }

    /// <summary>Where PATH finds <paramref name="name"/>: the first executable file of that name.</summary>
    public static string? Find(string name, IReadOnlyList<string> searchPath) =>
        searchPath.Select(dir => System.IO.Path.Combine(dir, name)).FirstOrDefault(IsExecutable);

    internal static bool IsExecutable(string path) =>
        !OperatingSystem.IsWindows() && File.Exists(path) && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;

    private static bool IsScript(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            Span<byte> start = stackalloc byte[2];
            return file.ReadAtLeast(start, 2, throwOnEndOfStream: false) == 2 && start is [(byte)'#', (byte)'!'];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
