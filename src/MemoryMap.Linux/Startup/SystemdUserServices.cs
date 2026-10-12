namespace MemoryMap.Linux.Startup;

/// <summary>One enabled user service, as <c>systemctl --user show</c> describes it.</summary>
internal sealed record ServiceUnit(string Id, string? Description, string? Program, string? Arguments, string? FragmentPath);

/// <summary>
/// The systemd user services enabled to start with the user's session, asked of
/// <c>systemctl --user</c> (no root). Listed only: changing one is systemctl's job (D033).
/// </summary>
internal static class SystemdUserServices
{
    internal static readonly string[] ListArgs =
        ["--user", "list-unit-files", "--type=service", "--state=enabled", "--no-legend", "--no-pager", "--plain"];

    internal static string[] ShowArgs(IEnumerable<string> units) =>
        ["--user", "show", "--no-pager", "-p", "Id,Description,ExecStart,FragmentPath", .. units];

    /// <summary>The services; empty without systemctl (a system without systemd).</summary>
    /// <exception cref="IOException">systemctl is there but could not answer (no user manager, or too slow).</exception>
    public static IReadOnlyList<ServiceUnit> Read(Func<string[], CommandResult?> systemctl)
    {
        if (systemctl(ListArgs) is not { } list)
        {
            return [];
        }
        Check(list);
        var units = UnitNames(list.Output);
        if (units.Count == 0)
        {
            return [];
        }
        var show = systemctl(ShowArgs(units)) ?? throw new IOException("systemctl --user show did not answer.");
        Check(show);
        return Units(show.Output);
    }

    private static void Check(CommandResult result)
    {
        if (result.ExitCode != 0)
        {
            var error = result.Error.Trim();
            throw new IOException(error.Length > 0 ? error : $"systemctl --user failed (exit code {result.ExitCode}).");
        }
    }

    /// <summary>The unit names of a list-unit-files listing. Templates (name@.service) are left
    /// out: what runs is an instance, which the listing does not name.</summary>
    internal static IReadOnlyList<string> UnitNames(string listing) =>
        [.. listing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', 2)[0])
            .Where(unit => unit.EndsWith(".service", StringComparison.Ordinal) && !unit.Contains("@.", StringComparison.Ordinal))];

    /// <summary>The units of a show listing: one block of Key=value lines per unit, blank lines between.</summary>
    internal static IReadOnlyList<ServiceUnit> Units(string show)
    {
        var units = new List<ServiceUnit>();
        foreach (var block in show.Replace("\r", "", StringComparison.Ordinal).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in block.Split('\n'))
            {
                var equals = line.IndexOf('=');
                if (equals > 0)
                {
                    keys.TryAdd(line[..equals], line[(equals + 1)..]);
                }
            }
            if (keys.GetValueOrDefault("Id") is not { Length: > 0 } id)
            {
                continue;
            }
            var exec = keys.GetValueOrDefault("ExecStart") ?? "";
            units.Add(new ServiceUnit(id, NonEmpty(keys.GetValueOrDefault("Description")),
                Field(exec, "path", "argv[]"), Field(exec, "argv[]", "ignore_errors"), NonEmpty(keys.GetValueOrDefault("FragmentPath"))));
        }
        return units;
    }

    // ExecStart reads "{ path=/usr/bin/x ; argv[]=/usr/bin/x -a ; ignore_errors=no ; … }"; with
    // several commands, the first is the program that stays running in most services. An
    // argument may hold " ; " itself, so each field ends where the next one's name begins.
    private static string? Field(string exec, string name, string next)
    {
        var start = exec.IndexOf(name + "=", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }
        start += name.Length + 1;
        var end = exec.IndexOf($" ; {next}=", start, StringComparison.Ordinal);
        return NonEmpty((end < 0 ? exec[start..] : exec[start..end]).Trim());
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
