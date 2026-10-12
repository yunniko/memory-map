using MemoryMap.Core;

namespace MemoryMap.Windows;

/// <summary>
/// Executable paths of running processes, asked of Windows once per process and remembered.
/// Only a refusal is remembered as unknown; any other failure (a process still starting) is
/// asked again.
/// </summary>
public sealed class WindowsProcessPaths : IProcessPaths
{
    private readonly ProcessPathCache _cache = new();

    public StringComparison Comparison => StringComparison.OrdinalIgnoreCase;

    public string? PathOf(ProcessSample process) =>
        _cache.PathOf(process, p => WindowsProcessActions.TryImagePath(p, out var found, out var failure)
            ? (found, true)
            : (null, failure?.Outcome == ActionOutcome.AccessDenied));
}
