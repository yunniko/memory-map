using System.Globalization;

namespace MemoryMap.Core;

public static class Format
{
    private const double Mib = 1024 * 1024;
    private const double Gib = Mib * 1024;

    /// <summary>Whole megabytes ("1,234 MB") below 10 GB; gigabytes ("12.3 GB") from 10 GB or when <paramref name="preferGb"/>.</summary>
    public static string Bytes(long bytes, bool preferGb = false) =>
        preferGb || bytes >= 10 * Gib
            ? (bytes / Gib).ToString("N1", CultureInfo.CurrentCulture) + " GB"
            : (bytes / Mib).ToString("N0", CultureInfo.CurrentCulture) + " MB";

    public static string Percent(long part, long whole) =>
        whole <= 0 ? "–" : (100.0 * part / whole).ToString("N1", CultureInfo.CurrentCulture) + "%";

    /// <summary>"1,234 MB · 7.5%": how every view states a size and its share of RAM.</summary>
    public static string SizeShare(long bytes, long total) => $"{Bytes(bytes)} · {Percent(bytes, total)}";
}
