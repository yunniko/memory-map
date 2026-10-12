using System.Collections.Concurrent;
using System.Globalization;

namespace MemoryMap.Linux;

/// <summary>
/// When processes started, as the sampler reports it. The process actions compute it the same
/// way, so a process's start time matches its sample exactly (D008).
/// </summary>
internal static class ProcClock
{
    // btime is derived from the clock and can read a second apart; one value per /proc keeps
    // the sampler and the actions agreeing until the next boot.
    private static readonly ConcurrentDictionary<string, DateTimeOffset> BootTimes = new();

    public static DateTimeOffset BootTime(IProcFs proc) =>
        proc is ProcFs real ? BootTimes.GetOrAdd(real.Root, _ => ReadBootTime(proc)) : ReadBootTime(proc);

    private static DateTimeOffset ReadBootTime(IProcFs proc)
    {
        foreach (var line in proc.ReadRequired("stat").AsSpan().EnumerateLines())
        {
            if (line.StartsWith("btime ") && long.TryParse(line[6..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
        }
        throw new InvalidDataException("/proc/stat has no btime line.");
    }

    public static DateTimeOffset StartTime(DateTimeOffset bootTime, long startTicks) =>
        bootTime.AddSeconds((double)startTicks / ProcText.TicksPerSecond).ToLocalTime();
}
