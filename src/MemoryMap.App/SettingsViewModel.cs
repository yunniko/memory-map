using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>
/// The Settings tab and the History tab's window picker, over one <see cref="AppSettings"/>.
/// Every change is normalised, saved to <see cref="Path"/> at once (there is no OK button) and
/// announced through <see cref="Changed"/>; a failed save is reported in <see cref="Problem"/>.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private string? _problem;

    /// <param name="path">Where to save; null keeps the settings in memory only (tests).</param>
    public SettingsViewModel(AppSettings? initial = null, string? path = null, string? loadProblem = null)
    {
        Current = (initial ?? new AppSettings()).Normalized();
        Path = path;
        _problem = loadProblem;
    }

    public event Action<AppSettings>? Changed;

    /// <summary>%APPDATA%\MemoryMap\settings.json.</summary>
    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MemoryMap", "settings.json");

    public AppSettings Current { get; private set; }

    public string? Path { get; }

    /// <summary>Why the settings could not be read or saved; null when all is well.</summary>
    public string? Problem { get => _problem; private set => Set(ref _problem, value); }

    public IReadOnlyList<string> RefreshLabels { get; } = [.. AppSettings.RefreshChoices.Select(AppSettings.RefreshLabel)];

    public IReadOnlyList<string> WindowLabels { get; } = [.. AppSettings.WindowChoices.Select(AppSettings.WindowLabel)];

    // A ComboBox reports -1 while its items are replaced; that is not a choice.
    public int RefreshIndex
    {
        get => IndexOf(AppSettings.RefreshChoices, Current.RefreshSeconds);
        set
        {
            if (value >= 0 && value < AppSettings.RefreshChoices.Count)
            {
                Update(Current with { RefreshSeconds = AppSettings.RefreshChoices[value] });
            }
        }
    }

    public int WindowIndex
    {
        get => IndexOf(AppSettings.WindowChoices, Current.HistoryMinutes);
        set
        {
            if (value >= 0 && value < AppSettings.WindowChoices.Count)
            {
                Update(Current with { HistoryMinutes = (int)AppSettings.WindowChoices[value] });
            }
        }
    }

    /// <summary>Decimal because that is what NumericUpDown binds; null (a cleared box) keeps the old value.</summary>
    public decimal? AlertBelowPercent
    {
        get => Current.AlertBelowPercent;
        set
        {
            if (value is { } v)
            {
                Update(Current with { AlertBelowPercent = (int)Math.Round(v) });
            }
            else
            {
                // Nothing changed, but the cleared box must show the value still in force.
                Raise(nameof(AlertBelowPercent));
            }
        }
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
            Problem = SettingsStore.TrySave(path, next);
        }
        // Every bound property derives from Current: one notification for all of them.
        Raise(string.Empty);
        Changed?.Invoke(next);
    }

    private static int IndexOf(IReadOnlyList<double> choices, double value)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (choices[i] == value)
            {
                return i;
            }
        }
        return 0;
    }
}
