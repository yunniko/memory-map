using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>
/// Draws a <see cref="HistoryChart"/>: recessive gridlines, one y-axis in bytes, 2 px lines in
/// their series colours, broken where sampling paused. The point under the pointer is reported
/// through <see cref="HoveredIndex"/> and marked with a crosshair; the window shows the tooltip.
/// </summary>
public sealed class HistoryChartControl : Control
{
    public static readonly StyledProperty<HistoryChart?> ChartProperty =
        AvaloniaProperty.Register<HistoryChartControl, HistoryChart?>(nameof(Chart));

    public static readonly DirectProperty<HistoryChartControl, int?> HoveredIndexProperty =
        AvaloniaProperty.RegisterDirect<HistoryChartControl, int?>(nameof(HoveredIndex), c => c.HoveredIndex);

    private const double LabelSize = 11;
    private const double Marker = 4;
    private static readonly Typeface Regular = new(FontFamily.Default);
    private static readonly Thickness Padding = new(8, 14, 12, 6);

    private int? _hovered;
    private Point? _pointer;
    private (HistoryChart? Chart, Size Size, Rect Plot) _plot;
    private (HistoryChart? Chart, Rect Plot, Geometry?[] Geometry) _lines = (null, default, []);

    static HistoryChartControl() => AffectsRender<HistoryChartControl>(ChartProperty);

    public HistoryChartControl()
    {
        ClipToBounds = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public HistoryChart? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    public int? HoveredIndex
    {
        get => _hovered;
        private set => SetAndRaise(HoveredIndexProperty, ref _hovered, value);
    }

    /// <summary>The plotting area inside the axis labels, in control coordinates.</summary>
    internal Rect Plot => PlotFor(Chart);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChartProperty)
        {
            // New points slide under a still pointer: hover whatever is there now.
            UpdateHover();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        UpdateHover();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        UpdateHover();
    }

    private void UpdateHover()
    {
        var plot = Plot;
        var index = _pointer is { } p && plot.Contains(p) ? Chart?.NearestIndex(p.X - plot.X, plot.Width) : null;
        if (index != _hovered)
        {
            InvalidateVisual();
        }
        HoveredIndex = index;
    }

    public override void Render(DrawingContext context)
    {
        var palette = MapPalette.For(this, ActualThemeVariant);
        context.DrawRectangle(palette.Surface, new Pen(palette.Border), new Rect(Bounds.Size).Deflate(0.5), 8, 8);
        if (Chart is not { } chart)
        {
            return;
        }
        var plot = PlotFor(chart);
        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        foreach (var tick in chart.YTicks())
        {
            var y = Math.Round(plot.Y + chart.Y(tick.At, plot.Height)) + 0.5;
            context.DrawLine(tick.At == 0 ? palette.Baseline : palette.Grid, new Point(plot.X, y), new Point(plot.Right, y));
            var label = Text(tick.Label, palette.Ink2);
            context.DrawText(label, new Point(plot.X - 8 - label.Width, y - (label.Height / 2)));
        }
        foreach (var tick in chart.XTicks(TimeZoneInfo.Local))
        {
            var label = Text(tick.Label, palette.Ink2);
            var x = plot.X + chart.X(tick.At, plot.Width);
            context.DrawText(label, new Point(Math.Clamp(x - (label.Width / 2), plot.X, plot.Right - label.Width), plot.Bottom + 4));
        }

        if (chart.IsEmpty)
        {
            var waiting = Text("Collecting history…", palette.Muted);
            context.DrawText(waiting, plot.Center - new Point(waiting.Width / 2, waiting.Height / 2));
            return;
        }

        using (context.PushClip(plot.Inflate(2)))
        {
            // In use last, so the headline line is never covered by an app's.
            for (var s = chart.Series.Count - 1; s >= 0; s--)
            {
                context.DrawGeometry(null, palette.SeriesPen(chart.Series[s].Slot), Line(chart, s, plot));
            }
            if (_hovered is { } i && i < chart.Points.Count)
            {
                var x = plot.X + chart.X(chart.Points[i].Taken, plot.Width);
                context.DrawLine(palette.Baseline, new Point(x, plot.Y), new Point(x, plot.Bottom));
                var ring = new Pen(palette.Surface, 2);
                foreach (var series in chart.Series)
                {
                    var at = new Point(x, plot.Y + chart.Y(series.Values[i], plot.Height));
                    context.DrawEllipse(palette.Series(series.Slot), ring, at, Marker, Marker);
                }
            }
        }
    }

    // Cached per chart and plot: hovering redraws every pointer move, but the lines change only
    // with a new chart (once a sample) or a new size.
    private Geometry Line(HistoryChart chart, int s, Rect plot)
    {
        if (!ReferenceEquals(chart, _lines.Chart) || plot != _lines.Plot)
        {
            _lines = (chart, plot, new Geometry?[chart.Series.Count]);
        }
        return _lines.Geometry[s] ??= BuildLine(chart, chart.Series[s], plot);
    }

    // A run of points is one polyline; a gap starts a new figure, and a lone point is a dot.
    private static StreamGeometry BuildLine(HistoryChart chart, ChartSeries series, Rect plot)
    {
        Point At(int i) => new(plot.X + chart.X(chart.Points[i].Taken, plot.Width), plot.Y + chart.Y(series.Values[i], plot.Height));
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var start = 0; start < chart.Points.Count;)
            {
                var end = start + 1;
                while (end < chart.Points.Count && !chart.BreaksBefore(end))
                {
                    end++;
                }
                g.BeginFigure(At(start), false);
                // A lone point gets a zero-length segment: the round cap draws it as a dot.
                g.LineTo(end - start == 1 ? At(start) : At(start + 1));
                for (var i = start + 2; i < end; i++)
                {
                    g.LineTo(At(i));
                }
                g.EndFigure(false);
                start = end;
            }
        }
        return geometry;
    }

    // Cached: the hover asks on every pointer move, and the answer changes only with the chart or size.
    private Rect PlotFor(HistoryChart? chart)
    {
        if (!ReferenceEquals(chart, _plot.Chart) || Bounds.Size != _plot.Size)
        {
            _plot = (chart, Bounds.Size, MeasurePlot(chart));
        }
        return _plot.Plot;
    }

    private Rect MeasurePlot(HistoryChart? chart)
    {
        var labelWidth = chart is null ? 0 : chart.YTicks().Max(t => Text(t.Label, Brushes.Black).Width);
        var bottom = Text("00:00", Brushes.Black).Height + 6;
        var inner = new Rect(Bounds.Size).Deflate(Padding);
        return new Rect(inner.X + labelWidth + 8, inner.Y, Math.Max(0, inner.Width - labelWidth - 8), Math.Max(0, inner.Height - bottom));
    }

    private static FormattedText Text(string text, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Regular, LabelSize, brush);
}
