namespace MemoryMap.Windows.Startup;

/// <summary>Which program a registered command line runs, by Windows' own rules for it.</summary>
internal static class CommandLine
{
    /// <summary>
    /// The program file <paramref name="command"/> starts, with environment variables expanded;
    /// null for an empty command. A quoted first token is the program. Unquoted, Windows tries
    /// the shortest space-separated prefix that is a file, so "C:\Program Files\A B\x.exe -s"
    /// still finds x.exe; when no prefix exists, the text up to ".exe" is taken. A bare name is
    /// looked up in the system folders and PATH. The result may not exist: callers check.
    /// </summary>
    public static string? Executable(string command, Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var text = Environment.ExpandEnvironmentVariables(command).Trim();
        if (text.Length == 0)
        {
            return null;
        }
        string program;
        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            program = close < 0 ? text[1..] : text[1..close];
        }
        else
        {
            program = FirstExisting(text, fileExists) ?? UpToExe(text);
        }
        program = program.Trim();
        return program.Length == 0 ? null : Locate(program, fileExists);
    }

    private static string? FirstExisting(string text, Func<string, bool> fileExists)
    {
        for (var space = text.IndexOf(' '); ; space = text.IndexOf(' ', space + 1))
        {
            var candidate = space < 0 ? text : text[..space];
            if (fileExists(candidate))
            {
                return candidate;
            }
            if (!Path.HasExtension(candidate) && fileExists(candidate + ".exe"))
            {
                return candidate + ".exe";
            }
            if (space < 0)
            {
                return null;
            }
        }
    }

    private static string UpToExe(string text)
    {
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0 && (exe + 4 == text.Length || text[exe + 4] == ' '))
        {
            return text[..(exe + 4)];
        }
        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    // A bare name ("ctfmon" or "rundll32.exe") is found where CreateProcess looks for it.
    private static string Locate(string program, Func<string, bool> fileExists)
    {
        if (Path.IsPathFullyQualified(program))
        {
            return program;
        }
        if (Path.IsPathRooted(program))
        {
            // "\tools\x.exe" or "C:x.exe": relative to the drive of the folder Explorer starts
            // programs in (System32), not searched for.
            return Path.GetFullPath(program, Environment.SystemDirectory);
        }
        var name = Path.HasExtension(program) ? program : program + ".exe";
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        IEnumerable<string> folders =
        [
            Environment.SystemDirectory,
            windows,
            .. path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ];
        foreach (var folder in folders)
        {
            try
            {
                var candidate = Path.Combine(folder, name);
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // A PATH entry with characters no path may hold; Windows skips it too.
            }
        }
        return name;
    }
}
