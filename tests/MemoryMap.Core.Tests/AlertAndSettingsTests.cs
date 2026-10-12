using MemoryMap.Core;
using static MemoryMap.Core.Tests.TestData;

namespace MemoryMap.Core.Tests;

public class AlertAndSettingsTests
{
    private static SystemMemory Available(double percent) => Sys(totalGb: 16, availableMb: (long)(16 * 1024 * percent / 100));

    [Fact]
    public void Warns_once_on_the_way_down_and_rearms_only_after_a_margin()
    {
        var alert = new LowMemoryAlert();

        Assert.Equal(AlertChange.None, alert.Update(Available(15), 10, true));
        Assert.Equal(AlertChange.Raised, alert.Update(Available(9), 10, true));
        Assert.Equal(AlertChange.None, alert.Update(Available(8), 10, true));
        Assert.Equal(AlertChange.None, alert.Update(Available(11), 10, true)); // above the line, inside the margin
        Assert.Equal(AlertChange.None, alert.Update(Available(9.5), 10, true)); // no second warning
        Assert.Equal(AlertChange.Cleared, alert.Update(Available(12.5), 10, true));
        Assert.Equal(AlertChange.Raised, alert.Update(Available(9), 10, true));
    }

    [Fact]
    public void Turning_alerts_off_clears_an_active_one_and_raises_nothing()
    {
        var alert = new LowMemoryAlert();
        alert.Update(Available(5), 10, true);

        Assert.Equal(AlertChange.Cleared, alert.Update(Available(5), 10, false));
        Assert.Equal(AlertChange.None, alert.Update(Available(1), 10, false));
        Assert.False(alert.IsActive);
    }

    [Fact]
    public void The_alert_names_how_low_memory_is_and_the_biggest_app()
    {
        var b = Breakdown.Build(Snapshot(Sys(availableMb: 1024), Proc(1, "chrome", 3000), Proc(2, "code", 1500)));

        var text = LowMemoryAlert.Message(b, 10);

        Assert.Equal($"Only {Format.Bytes(Gb, true)} of RAM is available ({Format.Percent(Gb, 16 * Gb)}), below the 10% alert. "
            + $"The biggest app is chrome at {Format.Bytes(3000 * Mb)}.", text);
    }

    [Fact]
    public void The_tray_tip_gives_the_share_in_use()
    {
        var tip = LowMemoryAlert.TrayTip(Sys(availableMb: 4096));

        Assert.StartsWith($"Memory Map: {Format.Percent(12 * Gb, 16 * Gb)} in use", tip, StringComparison.Ordinal);
        Assert.True(tip.Length < 128, "Windows cuts tray tooltips at 127 characters");
    }

    [Fact]
    public void Settings_round_trip_and_a_missing_file_means_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"memorymap-{Guid.NewGuid():N}", "settings.json");
        try
        {
            Assert.Equal((new AppSettings(), (string?)null), SettingsStore.Load(path));

            var changed = new AppSettings { RefreshSeconds = 2, AlertBelowPercent = 15, AlertsEnabled = false, CloseToTray = false, HistoryMinutes = 60 };
            SettingsStore.Save(path, changed);

            Assert.Equal((changed, (string?)null), SettingsStore.Load(path));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void A_broken_or_out_of_range_settings_file_is_corrected_not_fatal()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"memorymap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var broken = Path.Combine(dir, "broken.json");
            File.WriteAllText(broken, "{ not json");
            var (settings, problem) = SettingsStore.Load(broken);
            Assert.Equal(new AppSettings(), settings);
            Assert.StartsWith("Settings could not be read", problem, StringComparison.Ordinal);

            var odd = Path.Combine(dir, "odd.json");
            File.WriteAllText(odd, """{ "RefreshSeconds": 0.1, "AlertBelowPercent": 99, "HistoryMinutes": 40 }""");
            var (fixedUp, none) = SettingsStore.Load(odd);
            Assert.Null(none);
            Assert.Equal(0.5, fixedUp.RefreshSeconds);
            Assert.Equal(AppSettings.MaxAlertPercent, fixedUp.AlertBelowPercent);
            Assert.Contains(fixedUp.HistoryMinutes, AppSettings.WindowChoices);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
