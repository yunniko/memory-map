using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>A line of the chart legend: its colour slot, name and newest value.</summary>
public sealed record SeriesLegendItem(int Slot, string Name, string Latest);

/// <summary>
/// The History tab: keeps every sample in <see cref="MemoryHistory"/> (always, so the chart has
/// data whenever it is opened) and builds the <see cref="Chart"/> only when asked, which
/// <see cref="MainViewModel"/> does while the tab is on screen.
/// </summary>
public sealed class HistoryViewModel : ObservableObject
{
    private readonly SeriesSlots _slots = new();
    private HistoryChart? _chart;
    private IReadOnlyList<SeriesLegendItem> _legend = [];
    private HoverTip? _hover;
    private int? _hoverIndex;

    public MemoryHistory History { get; } = new();

    /// <summary>How far back the chart reaches.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(30);

    public HistoryChart? Chart { get => _chart; private set => Set(ref _chart, value); }

    public IReadOnlyList<SeriesLegendItem> Legend { get => _legend; private set => SetList(ref _legend, value); }

    /// <summary>The tooltip for the hovered point: its time and every line's value.</summary>
    public HoverTip? Hover { get => _hover; private set => Set(ref _hover, value); }

    public void Rebuild()
    {
        var chart = HistoryChart.Build(History, Window, _slots);
        Chart = chart;
        Legend = chart.IsEmpty
            ? []
            : [.. chart.Series.Select(s => new SeriesLegendItem(s.Slot, s.Name, Format.Bytes(s.Values[^1])))];
        SetHover(_hoverIndex);
    }

    /// <summary>Drops the chart while it is not on screen; the history itself is kept.</summary>
    public void Release()
    {
        Chart = null;
        Legend = [];
        SetHover(null);
    }

    public void SetHover(int? index)
    {
        _hoverIndex = index;
        if (index is not { } i || Chart is not { } chart || i >= chart.Points.Count)
        {
            Hover = null;
            return;
        }
        var (title, lines) = chart.Describe(i);
        Hover = new HoverTip(title, string.Join(Environment.NewLine, lines));
    }
}
