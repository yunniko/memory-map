namespace MemoryMap.Linux;

/// <summary>
/// Read access to <c>/proc</c>. Paths are relative to it ("meminfo", "42/status"). A file that
/// is gone or unreadable reads as null: processes exit and other users' files are closed to us
/// between listing and reading, and neither is an error.
/// </summary>
public interface IProcFs
{
    string? ReadText(string path);

    /// <summary>The target of a symbolic link such as "42/exe"; null if absent or not permitted.</summary>
    string? ReadLink(string path);

    /// <summary>The numeric entries of /proc: one per process.</summary>
    IEnumerable<int> Pids();
}

/// <summary>The real <c>/proc</c>.</summary>
public sealed class ProcFs(string root = "/proc") : IProcFs
{
    public string? ReadText(string path) => OrNull(() => File.ReadAllText(Path.Combine(root, path)));

    public string? ReadLink(string path) => OrNull(() => new FileInfo(Path.Combine(root, path)).LinkTarget);

    private static string? OrNull(Func<string?> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public IEnumerable<int> Pids()
    {
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            if (int.TryParse(Path.GetFileName(dir), out var pid))
            {
                yield return pid;
            }
        }
    }
}
