using MemoryMap.Core;

namespace MemoryMap.Linux;

/// <summary>
/// The file each process runs, from <c>/proc/&lt;pid&gt;/exe</c>, for matching startup entries.
/// Another user's processes do not say (their link is not readable without root), and a kernel
/// thread has none: both are null, and asked again, since a process just starting may answer later.
/// </summary>
public sealed class LinuxProcessPaths(IProcFs? proc = null) : IProcessPaths
{
    private readonly IProcFs _proc = proc ?? new ProcFs();
    private readonly ProcessPathCache _cache = new();

    public StringComparison Comparison => StringComparison.Ordinal;

    public string? PathOf(ProcessSample process) =>
        _cache.PathOf(process, p => _proc.ReadLink($"{p.Pid}/exe") is { Length: > 0 } link ? (ProcText.ExePath(link), true) : (null, false));
}
