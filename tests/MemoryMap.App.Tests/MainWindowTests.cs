using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MemoryMap.Core;
using static MemoryMap.App.Tests.Helpers.Samples;

namespace MemoryMap.App.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public void Window_shows_summary_legend_and_a_tile_for_every_map_item()
    {
        var b = Breakdown();
        var (window, _) = Open(b);

        Assert.StartsWith("In use 12.0 GB of 16.0 GB", window.SummaryText.Text);
        Assert.Equal(6, window.LegendList.ItemCount);
        Assert.Equal(b.Groups.SelectMany(g => g.Children).Select(c => c.Key).Order(), window.Map.Scene.Tiles.Select(t => t.Node.Key).Order());
        Assert.All(window.Map.Scene.Tiles, t => Assert.True(t.Rect.Width > 0 && t.Rect.Height > 0));
        Capture(window, "map-light");
    }

    [AvaloniaFact]
    public void Hovering_a_tile_shows_its_name_size_share_and_pids()
    {
        var (window, model) = Open(Breakdown());
        var chrome = window.Map.Scene.Tiles.Single(t => t.Node.Name == "chrome");

        Hover(window, chrome.Rect);

        Assert.True(window.Tip.IsVisible);
        Assert.Equal("chrome", window.TipTitle.Text);
        Assert.Contains(Format.Bytes(3400 * Mb), window.TipText.Text, StringComparison.Ordinal);
        Assert.Contains("of RAM", window.TipText.Text);
        Assert.Contains("PIDs 10, 11", window.TipText.Text);
        Capture(window, "map-hover");

        window.MouseMove(new Point(-10, -10));
        Dispatcher.UIThread.RunJobs();
        Assert.Null(model.Hover);
    }

    [AvaloniaFact]
    public void A_refresh_with_small_changes_keeps_tiles_in_place_and_the_hover()
    {
        var (window, model) = Open(Breakdown(chromeMb: 3000, codeMb: 2900));
        var before = window.Map.Scene.Tiles.Select(t => t.Node.Key).ToList();
        var code = window.Map.Scene.Tiles.Single(t => t.Node.Name == "code");
        Hover(window, code.Rect);

        model.Apply(Breakdown(chromeMb: 2900, codeMb: 3000));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, window.Map.Scene.Tiles.Select(t => t.Node.Key));
        Assert.Equal("code", window.Map.HoveredNode?.Name);
        Assert.Equal(3000 * Mb, window.Map.HoveredNode!.Bytes);
    }

    [AvaloniaFact]
    public void After_a_refresh_the_hover_is_whatever_is_under_the_pointer()
    {
        var (window, model) = Open(Breakdown());
        var chrome = window.Map.Scene.Tiles.Single(t => t.Node.Name == "chrome");
        Hover(window, chrome.Rect);
        var at = new Point(chrome.Rect.X + (chrome.Rect.Width / 2), chrome.Rect.Y + (chrome.Rect.Height / 2));

        model.Apply(Breakdown(chromeMb: 0)); // the big chrome process exited; its tile shrinks
        Dispatcher.UIThread.RunJobs();

        var under = window.Map.Scene.HitTest(at.X, at.Y);
        Assert.NotNull(under);
        Assert.NotEqual("chrome", under.Name);
        Assert.Equal(under?.Key, window.Map.HoveredNode?.Key);
        Assert.Equal(under?.Name, model.Hover?.Title);
    }

    [AvaloniaFact]
    public void An_unchanged_legend_keeps_its_items()
    {
        var (_, model) = Open(Breakdown());
        var legend = model.Legend;

        model.Apply(Breakdown());

        Assert.Same(legend, model.Legend);
    }

    [AvaloniaFact]
    public void Table_lists_the_same_items_as_the_map()
    {
        var b = Breakdown();
        var (window, model) = Open(b);
        Assert.Empty(model.Rows); // not built while the map is shown

        window.Views.SelectedIndex = MainViewModel.TableView;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Describe.Table(b).Select(r => r.Key), window.TableList.Items.Cast<TableRow>().Select(r => r.Key));
        Assert.True(window.TableList.GetRealizedContainers().Any());
        Capture(window, "table-light");
    }

    [AvaloniaFact]
    public void A_failed_sample_keeps_the_last_map_and_says_so()
    {
        var b = Breakdown();
        var (window, model) = Open(b);

        model.ShowFailure(new InvalidOperationException("Counter returned status 0xC0000BC6."), DateTimeOffset.Now);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(b.Root, window.Map.Root);
        Assert.True(model.IsStale);
        Assert.Contains("0xC0000BC6", window.StatusText.Text);
        Assert.Contains("showing the last good sample", window.StatusText.Text);
    }

    [AvaloniaFact]
    public void Live_updates_from_the_sampler_reach_the_window()
    {
        using var sampler = new OneSnapshotSampler(Snapshot());
        using var live = new LiveMemory(sampler, TimeSpan.FromSeconds(1));
        var model = new MainViewModel(live);
        var window = new MainWindow { DataContext = model };
        window.Show();

        Assert.True(live.RefreshNow());
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(window.Map.Root);
        Assert.StartsWith("Updated", window.StatusText.Text);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Tiles_use_the_theme_palette(string variant)
    {
        var theme = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var (window, _) = Open(Breakdown(), theme);
        var chrome = window.Map.Scene.Tiles.Single(t => t.Node.Name == "chrome");
        var frame = Capture(window, $"map-{variant.ToLowerInvariant()}-theme");

        // A point inside the tile, clear of its colour bar and label.
        var at = window.Map.TranslatePoint(new Point(chrome.Rect.Right - 6, chrome.Rect.Bottom - 6), window)!.Value;
        var pixel = PixelAt(frame, (int)at.X, (int)at.Y);
        var expected = variant == "Dark" ? Mix(0x1a1a19, 0x3987e5, 0.30) : Mix(0xfcfcfb, 0x2a78d6, 0.20);

        Assert.True(Close(pixel, expected), $"tile pixel {pixel} is not the {variant} apps fill {expected}");
    }

    private static void Hover(MainWindow window, LayoutRect rect)
    {
        var inMap = new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
        window.MouseMove(window.Map.TranslatePoint(inMap, window)!.Value);
        Dispatcher.UIThread.RunJobs();
    }

    // Deliberately independent of MapPalette's mix: it is the oracle the pixel is checked against.
    private static Color Mix(uint surface, uint group, double t)
    {
        static byte Lerp(uint a, uint b, double t) => (byte)Math.Round(a + ((b - (double)a) * t));
        return Color.FromRgb(
            Lerp((surface >> 16) & 0xff, (group >> 16) & 0xff, t),
            Lerp((surface >> 8) & 0xff, (group >> 8) & 0xff, t),
            Lerp(surface & 0xff, group & 0xff, t));
    }

    private static bool Close(Color a, Color b) =>
        Math.Abs(a.R - b.R) <= 3 && Math.Abs(a.G - b.G) <= 3 && Math.Abs(a.B - b.B) <= 3;

    private sealed class OneSnapshotSampler(MemorySnapshot snapshot) : IMemorySampler
    {
        public MemorySnapshot Sample() => snapshot;

        public void Dispose()
        {
        }
    }
}
