using System.Text.Json;

namespace MemoryMap.Core;

/// <summary>What the user can set. Out-of-range values from a hand-edited file are pulled back
/// into range by <see cref="Normalized"/> rather than rejected.</summary>
public sealed record AppSettings
{
    public static readonly IReadOnlyList<double> RefreshChoices = [0.5, 1, 2, 5];

    /// <summary>The refresh while the window is hidden or minimised, in seconds; 0 (the default)
    /// keeps the shown rate. Nothing is drawn then, so only the tray, alert and history need samples (D016).</summary>
    public static readonly IReadOnlyList<double> HiddenRefreshChoices = [0, 5, 10, 30];

    /// <summary>The history chart's windows, in minutes; the history keeps the longest.</summary>
    public static readonly IReadOnlyList<double> WindowChoices = [5, 15, 30, 60];
    public const int MinAlertPercent = 1;
    public const int MaxAlertPercent = 50;

    public double RefreshSeconds { get; init; } = 1;

    /// <summary>One of <see cref="HiddenRefreshChoices"/>; see <see cref="IntervalSeconds"/>.</summary>
    public double HiddenRefreshSeconds { get; init; }

    /// <summary>Warn when available memory falls below this share of RAM.</summary>
    public int AlertBelowPercent { get; init; } = 10;

    public bool AlertsEnabled { get; init; } = true;

    /// <summary>Closing the window hides it to the tray; Exit is on the tray menu.</summary>
    public bool CloseToTray { get; init; } = true;

    /// <summary>The history chart's window, one of <see cref="WindowChoices"/>.</summary>
    public int HistoryMinutes { get; init; } = 30;

    public static string RefreshLabel(double seconds) => seconds.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " s";

    public static string HiddenRefreshLabel(double seconds) => seconds == 0 ? "Same as shown" : "Every " + RefreshLabel(seconds);

    public static string WindowLabel(double minutes) => $"Last {minutes} minutes";

    /// <summary>The slowest refresh any setting can produce.</summary>
    public static double SlowestRefreshSeconds => Math.Max(RefreshChoices.Max(), HiddenRefreshChoices.Max());

    /// <summary>Seconds between samples. Hiding the window never samples faster than showing it.</summary>
    public double IntervalSeconds(bool onScreen) =>
        onScreen || HiddenRefreshSeconds == 0 ? RefreshSeconds : Math.Max(RefreshSeconds, HiddenRefreshSeconds);

    public AppSettings Normalized() => this with
    {
        RefreshSeconds = Nearest(RefreshChoices, RefreshSeconds),
        HiddenRefreshSeconds = Nearest(HiddenRefreshChoices, HiddenRefreshSeconds),
        AlertBelowPercent = Math.Clamp(AlertBelowPercent, MinAlertPercent, MaxAlertPercent),
        HistoryMinutes = (int)Nearest(WindowChoices, HistoryMinutes),
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

    /// <summary>Saves, or says why it could not; the settings stay in force either way.</summary>
    public static string? TrySave(string path, AppSettings settings)
    {
        try
        {
            Save(path, settings);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"Settings could not be saved, so they last until Memory Map closes ({e.Message}).";
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
