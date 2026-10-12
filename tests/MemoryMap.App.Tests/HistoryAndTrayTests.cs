using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MemoryMap.Core;
using static MemoryMap.App.Tests.Helpers.Samples;

namespace MemoryMap.App.Tests;

public class HistoryAndTrayTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 16, 0, 0, TimeSpan.Zero);

    private static MemoryBreakdown At(int second, long chromeMb = 3000, long availableMb = 4096) =>
        Core.Breakdown.Build(Snapshot(chromeMb: chromeMb, availableMb: availableMb) with { Taken = T0.AddSeconds(second) });

    // Five minutes of samples in which chrome grows, so the lines have a shape.
    private static (MainWindow Window, MainViewModel Model) OpenHistory(ThemeVariant? theme = null)
    {
        var (window, model) = Open(theme: theme);
        model.ViewIndex = MainViewModel.HistoryView;
        model.Settings.WindowIndex = 0; // 5 minutes
        for (var s = 0; s <= 300; s++)
        {
            model.Apply(At(s, chromeMb: 2000 + (s * 5)));
        }
        Dispatcher.UIThread.RunJobs();
        return (window, model);
    }

    [AvaloniaFact]
    public void The_history_tab_draws_every_line_with_a_legend_of_newest_values()
    {
        var (window, model) = OpenHistory();

        var chart = window.Chart.Chart!;
        Assert.Equal(301, chart.Points.Count);
        Assert.Equal(["In use", "Kernel pools", "chrome", "vmmemWSL", "code", "firefox", "svchost"], chart.Series.Select(s => s.Name));
        Assert.Equal(chart.Series.Count, window.SeriesLegend.ItemCount);
        Assert.Equal(Format.Bytes(3900 * Mb), model.History.Legend.Single(l => l.Name == "chrome").Latest);

        // The In use line is drawn in its slot colour, at its newest value at the right edge.
        var plot = window.Chart.Plot;
        var frame = Capture(window, "history-light");
        var origin = window.Chart.TranslatePoint(plot.TopLeft, window)!.Value;
        var y = origin.Y + chart.Y(chart.Series[0].Values[^1], plot.Height);
        Assert.Equal(Color.Parse("#2a78d6"), PixelAt(frame, (int)(origin.X + plot.Width - 3), (int)Math.Round(y)));
    }

    [AvaloniaFact]
    public void Hovering_the_chart_lists_every_line_at_that_time()
    {
        var (window, model) = OpenHistory();
        var plot = window.Chart.Plot;

        window.MouseMove(window.Chart.TranslatePoint(new Point(plot.X + (plot.Width / 2), plot.Y + 20), window)!.Value);
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.ChartTip.IsVisible);
        Assert.InRange(window.Chart.HoveredIndex!.Value, 148, 152);
        var lines = window.ChartTipText.Text!.Split(Environment.NewLine);
        Assert.Equal(7, lines.Length);
        Assert.StartsWith("In use  12.0 GB", lines[0], StringComparison.Ordinal);
        Capture(window, "history-hover");

        window.MouseMove(new Point(-10, -10));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(model.History.Hover);
    }

    [AvaloniaFact]
    public void The_dark_chart_uses_the_dark_series_colours()
    {
        var (window, _) = OpenHistory(ThemeVariant.Dark);
        var chart = window.Chart.Chart!;
        var plot = window.Chart.Plot;

        var frame = Capture(window, "history-dark");
        var origin = window.Chart.TranslatePoint(plot.TopLeft, window)!.Value;
        var y = origin.Y + chart.Y(chart.Series[0].Values[^1], plot.Height);
        Assert.Equal(Color.Parse("#3987e5"), PixelAt(frame, (int)(origin.X + plot.Width - 3), (int)Math.Round(y)));
    }

    [AvaloniaFact]
    public void Samples_are_recorded_on_every_tab_but_the_chart_is_built_only_on_its_own()
    {
        var (_, model) = Open();
        for (var s = 0; s < 10; s++)
        {
            model.Apply(At(s));
        }

        Assert.Equal(10, model.History.History.Count);
        Assert.Null(model.History.Chart);

        model.ViewIndex = MainViewModel.HistoryView;
        Assert.Equal(10, model.History.Chart!.Points.Count);

        model.ViewIndex = MainViewModel.MapView;
        Assert.Null(model.History.Chart);
    }

    [AvaloniaFact]
    public void Settings_apply_at_once_and_are_saved()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"memorymap-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "settings.json");
        try
        {
            var settings = new SettingsViewModel(path: path);
            var model = new MainViewModel(settings: settings);
            var window = new MainWindow { DataContext = model };
            window.Show();
            model.ViewIndex = MainViewModel.SettingsView;
            Dispatcher.UIThread.RunJobs();

            window.RefreshPicker.SelectedIndex = 2;
            window.HiddenRefreshPicker.SelectedIndex = 3;
            window.AlertPercentBox.Value = 20;
            window.TrayBox.IsChecked = false;
            window.WindowPicker.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            Capture(window, "settings");

            var expected = new AppSettings { RefreshSeconds = 2, HiddenRefreshSeconds = 30, AlertBelowPercent = 20, CloseToTray = false, HistoryMinutes = 60 };
            Assert.Equal(expected, settings.Current);
            Assert.Equal((expected, (string?)null), SettingsStore.Load(path));
            Assert.Equal(TimeSpan.FromMinutes(60), model.History.Window);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public void Raising_the_threshold_above_what_is_available_raises_the_alert()
    {
        var (_, model) = Open(At(0, availableMb: 2048)); // 12.5% available

        Assert.False(model.IsLowMemory);
        model.Settings.AlertBelowPercent = 15;

        Assert.True(model.IsLowMemory);
        Assert.StartsWith("Only 2.0 GB of RAM is available", model.AlertMessage, StringComparison.Ordinal);

        model.Settings.AlertsEnabled = false;
        Assert.False(model.IsLowMemory);
    }

    [AvaloniaFact]
    public void Low_memory_opens_one_alert_that_closes_when_memory_recovers()
    {
        var (window, model) = Open(At(0));
        using var tray = new TrayController(Application.Current!, model, window.Reveal, () => { });

        model.Apply(At(1, availableMb: 1000));
        Dispatcher.UIThread.RunJobs();
        var alert = tray.Alert!;
        Assert.True(alert.IsVisible);
        Assert.Contains("below the 10% alert", alert.Text, StringComparison.Ordinal);
        Assert.Contains("chrome", alert.Text, StringComparison.Ordinal);
        Capture(alert, "alert");

        model.Apply(At(2, availableMb: 900));
        Assert.Same(alert, tray.Alert);

        model.Apply(At(3, availableMb: 4096));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(tray.Alert);
        Assert.False(alert.IsVisible);
    }

    [AvaloniaFact]
    public void Show_map_on_the_alert_brings_the_window_back()
    {
        var (window, model) = Open(At(0));
        var shown = 0;
        using var tray = new TrayController(Application.Current!, model, () => shown++, () => { });
        model.Apply(At(1, availableMb: 1000));
        Dispatcher.UIThread.RunJobs();

        tray.Alert!.ShowButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, shown);
        Assert.Null(tray.Alert);
        Assert.True(window.IsVisible);
    }

    [AvaloniaFact]
    public void The_tray_icon_fills_to_the_share_in_use_and_turns_red_when_low()
    {
        var palette = MapPalette.For(Application.Current!, ThemeVariant.Dark);
        using var half = TrayController.Render(palette, 50, low: false);
        using var low = TrayController.Render(palette, 90, low: true);
        using var halfFrame = Readable(half);
        using var lowFrame = Readable(low);

        Assert.Equal(Color.Parse("#3987e5"), PixelAt(halfFrame, 16, 26));
        Assert.Equal(Color.Parse("#383835"), PixelAt(halfFrame, 16, 8)); // above the level: the empty body
        Assert.Equal(Color.Parse("#d03b3b"), PixelAt(lowFrame, 16, 8));
    }

    [Theory]
    [InlineData(WindowCloseReason.WindowClosing, false, true, true, true)]
    [InlineData(WindowCloseReason.WindowClosing, true, true, true, false)] // Exit from the tray closes for real
    [InlineData(WindowCloseReason.WindowClosing, false, false, true, false)] // the setting is off
    [InlineData(WindowCloseReason.WindowClosing, false, true, false, false)] // no tray to come back from
    [InlineData(WindowCloseReason.OSShutdown, false, true, true, false)]
    [InlineData(WindowCloseReason.ApplicationShutdown, false, true, true, false)]
    public void Closing_hides_to_the_tray_only_for_the_users_own_close(WindowCloseReason reason, bool programmatic, bool setting, bool canHide, bool hides) =>
        Assert.Equal(hides, MainWindow.HidesInstead(reason, programmatic, setting, canHide));

    private static Avalonia.Media.Imaging.WriteableBitmap Readable(Avalonia.Media.Imaging.Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        stream.Position = 0;
        return Avalonia.Media.Imaging.WriteableBitmap.Decode(stream);
    }
}
