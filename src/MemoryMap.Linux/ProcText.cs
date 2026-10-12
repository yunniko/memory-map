using System.Globalization;

namespace MemoryMap.Linux;

/// <summary>Parsers for the text formats of /proc (proc(5)).</summary>
internal static class ProcText
{
    /// <summary>The kernel's USER_HZ: /proc reports times in these ticks, fixed at 100 by the ABI.</summary>
    public const int TicksPerSecond = 100;

    /// <summary>
    /// "Key:   value [kB]" lines (meminfo, status) as numbers, kB turned into bytes. Lines whose
    /// value does not start with a number (status' "Name:") are left out; for several numbers
    /// ("Uid:  1000 1000 1000 1000") the first is kept.
    /// </summary>
    public static Dictionary<string, long> Fields(string text)
    {
        var fields = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in text.AsSpan().EnumerateLines())
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            var rest = line[(colon + 1)..].Trim();
            var end = rest.IndexOfAny(' ', '\t');
            var number = end < 0 ? rest : rest[..end];
            if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }
            var kb = end >= 0 && rest[end..].Trim().SequenceEqual("kB");
            fields[line[..colon].ToString()] = kb ? value * 1024 : value;
        }
        return fields;
    }

    /// <summary>The fields of /proc/[pid]/stat this sampler uses; null if the text is malformed.</summary>
    public static StatLine? Stat(string text)
    {
        // The command name sits in parentheses and may itself contain spaces and parentheses:
        // the fields after it start after the last ')'.
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0 || close < open)
        {
            return null;
        }
        var fields = text[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // fields[0] is field 3 (state): flags is field 9, starttime field 22 (proc(5)).
        if (fields.Length < 20
            || !uint.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out var flags)
            || !long.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var start))
        {
            return null;
        }
        return new StatLine(text[(open + 1)..close], flags, start);
    }

    /// <summary>The first number of a text such as /proc/uptime ("12345.67 4321.00").</summary>
    public static double FirstNumber(string text)
    {
        var token = text.AsSpan().Trim();
        var end = token.IndexOfAny(' ', '\n');
        return double.Parse(end < 0 ? token : token[..end], NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}

/// <param name="Comm">The kernel's name for the process: the executable's file name cut to 15 characters,
/// unless the process renamed itself.</param>
/// <param name="StartTicks">When the process started, in <see cref="ProcText.TicksPerSecond"/> ticks after boot.</param>
internal sealed record StatLine(string Comm, uint Flags, long StartTicks)
{
    private const uint KernelThreadFlag = 0x00200000; // PF_KTHREAD

    public bool IsKernelThread => (Flags & KernelThreadFlag) != 0;
}
