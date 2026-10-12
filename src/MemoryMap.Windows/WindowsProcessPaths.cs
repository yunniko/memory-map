using MemoryMap.Core;

namespace MemoryMap.Windows;

/// <summary>
/// Executable paths of running processes, asked of Windows once per process and remembered:
/// a process's file never changes, and (PID, start time) names one process for good. Only a
/// refusal is remembered as unknown; any other failure (a process still starting) is asked again.
/// </summary>
public sealed class WindowsProcessPaths : IProcessPaths
{
    // Enough for every process a long session sees between clean-outs; cleared when full
    // rather than tracked per process, since asking again only costs one query.
    private const int Capacity = 4096;
    private readonly Dictionary<(int Pid, long Start), string?> _known = [];
    private readonly Lock _gate = new();

    public StringComparison Comparison => StringComparison.OrdinalIgnoreCase;

    public string? PathOf(ProcessSample process)
    {
        var key = (process.Pid, process.StartTime.UtcTicks);
        lock (_gate)
        {
            if (_known.TryGetValue(key, out var known))
            {
                return known;
            }
        }
        var path = WindowsProcessActions.TryImagePath(process, out var found, out var failure) ? found : null;
        if (path is null && failure?.Outcome != ActionOutcome.AccessDenied)
        {
            return null;
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
