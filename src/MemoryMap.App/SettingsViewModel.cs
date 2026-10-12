using System.ComponentModel;
using System.Globalization;
using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>
/// The Settings tab and the History tab's window picker, over one <see cref="AppSettings"/>.
/// Every change is normalised, saved to <see cref="Path"/> at once (there is no OK button) and
/// announced through <see cref="Changed"/>; a failed save is reported in <see cref="Problem"/>.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private string? _problem;

    /// <param name="path">Where to save; null keeps the settings in memory only (tests).</param>
    public SettingsViewModel(AppSettings? initial = null, string? path = null, string? loadProblem = null)
    {
        Current = (initial ?? new AppSettings()).Normalized();
        Path = path;
        _problem = loadProblem;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<AppSettings>? Changed;

    /// <summary>%APPDATA%\MemoryMap\settings.json.</summary>
    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MemoryMap", "settings.json");

    public AppSettings Current { get; private set; }

    public string? Path { get; }

    /// <summary>Why the settings could not be read or saved; null when all is well.</summary>
    public string? Problem
    {
        get => _problem;
        private set
        {
            if (_problem != value)
            {
                _problem = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Problem)));
            }
        }
    }

    public IReadOnlyList<string> RefreshLabels { get; } =
        [.. AppSettings.RefreshChoices.Select(s => s.ToString("0.#", CultureInfo.CurrentCulture) + " s")];

    public IReadOnlyList<string> WindowLabels { get; } = [.. HistoryChart.WindowChoices.Select(m => $"Last {m} minutes")];

    public int RefreshIndex
    {
        get => IndexOf(AppSettings.RefreshChoices, Current.RefreshSeconds);
        set => Update(Current with { RefreshSeconds = AppSettings.RefreshChoices[Math.Clamp(value, 0, AppSettings.RefreshChoices.Count - 1)] });
    }

    public int WindowIndex
    {
        get => IndexOf(HistoryChart.WindowChoices, Current.HistoryMinutes);
        set => Update(Current with { HistoryMinutes = HistoryChart.WindowChoices[Math.Clamp(value, 0, HistoryChart.WindowChoices.Count - 1)] });
    }

    /// <summary>Decimal because that is what NumericUpDown binds; null (a cleared box) keeps the old value.</summary>
    public decimal? AlertBelowPercent
    {
        get => Current.AlertBelowPercent;
        set => Update(Current with { AlertBelowPercent = value is { } v ? (int)Math.Round(v) : Current.AlertBelowPercent });
    }

    public bool AlertsEnabled { get => Current.AlertsEnabled; set => Update(Current with { AlertsEnabled = value }); }

    public bool CloseToTray { get => Current.CloseToTray; set => Update(Current with { CloseToTray = value }); }

    public static decimal MinAlertPercent => AppSettings.MinAlertPercent;

    public static decimal MaxAlertPercent => AppSettings.MaxAlertPercent;

    private void Update(AppSettings next)
    {
        next = next.Normalized();
        if (next == Current)
        {
            return;
        }
        Current = next;
        if (Path is { } path)
        {
            try
            {
                SettingsStore.Save(path, next);
                Problem = null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Problem = $"Settings could not be saved, so they last until Memory Map closes ({e.Message}).";
            }
        }
        // Every bound property derives from Current: one notification for all of them.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        Changed?.Invoke(next);
    }

    private static int IndexOf<T>(IReadOnlyList<T> choices, T value)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(choices[i], value))
            {
                return i;
            }
        }
        return 0;
    }
}
