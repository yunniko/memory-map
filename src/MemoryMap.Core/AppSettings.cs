using System.Text.Json;

namespace MemoryMap.Core;

/// <summary>What the user can set. Out-of-range values from a hand-edited file are pulled back
/// into range by <see cref="Normalized"/> rather than rejected.</summary>
public sealed record AppSettings
{
    public static readonly IReadOnlyList<double> RefreshChoices = [0.5, 1, 2, 5];
    public const int MinAlertPercent = 1;
    public const int MaxAlertPercent = 50;

    public double RefreshSeconds { get; init; } = 1;

    /// <summary>Warn when available memory falls below this share of RAM.</summary>
    public int AlertBelowPercent { get; init; } = 10;

    public bool AlertsEnabled { get; init; } = true;

    /// <summary>Closing the window hides it to the tray; Exit is on the tray menu.</summary>
    public bool CloseToTray { get; init; } = true;

    /// <summary>The history chart's window, one of <see cref="HistoryChart.WindowChoices"/>.</summary>
    public int HistoryMinutes { get; init; } = 30;

    public AppSettings Normalized() => this with
    {
        RefreshSeconds = Nearest(RefreshChoices, RefreshSeconds),
        AlertBelowPercent = Math.Clamp(AlertBelowPercent, MinAlertPercent, MaxAlertPercent),
        HistoryMinutes = (int)Nearest([.. HistoryChart.WindowChoices.Select(m => (double)m)], HistoryMinutes),
    };

    private static double Nearest(IReadOnlyList<double> choices, double value) =>
        double.IsFinite(value) ? choices.MinBy(c => Math.Abs(c - value)) : choices[0];
}

/// <summary>Reads and writes <see cref="AppSettings"/> as JSON. A missing file means defaults;
/// an unreadable one means defaults plus a problem to show, never a crash at startup.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static (AppSettings Settings, string? Problem) Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return (new AppSettings(), null);
            }
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new AppSettings();
            return (settings.Normalized(), null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new AppSettings(), $"Settings could not be read, so the defaults are in use ({e.Message}).");
        }
    }

    /// <summary>Writes beside the file and then swaps it in, so a crash mid-write cannot leave half a file.</summary>
    public static void Save(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, path, overwrite: true);
    }
}
