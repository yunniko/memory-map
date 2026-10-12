namespace MemoryMap.Core;

/// <summary>
/// Executable paths remembered per process: (PID, start time) names one process for good, and
/// its name with them one program (on Linux, exec() replaces the file and the name, not the
/// PID). Each OS's <see cref="IProcessPaths"/> asks its own way and keeps the answers here.
/// Thread-safe.
/// </summary>
public sealed class ProcessPathCache
{
    // Enough for every process a long session sees between clean-outs; cleared when full
    // rather than tracked per process, since asking again only costs one query.
    private const int Capacity = 4096;
    private readonly Dictionary<(int Pid, long Start, string Name), string?> _known = [];
    private readonly Lock _gate = new();

    /// <summary>The remembered path, or asks <paramref name="ask"/>: it returns the path (null if
    /// the OS will not say) and whether that answer holds for good.</summary>
    public string? PathOf(ProcessSample process, Func<ProcessSample, (string? Path, bool Lasting)> ask)
    {
        var key = (process.Pid, process.StartTime.UtcTicks, process.ImageName);
        lock (_gate)
        {
            if (_known.TryGetValue(key, out var known))
            {
                return known;
            }
        }
        var (path, lasting) = ask(process);
        if (!lasting)
        {
            return path;
        }
        lock (_gate)
        {
            if (_known.Count >= Capacity)
            {
                _known.Clear();
            }
            _known[key] = path;
        }
        return path;
    }
}
