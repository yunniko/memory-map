using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>
/// The map's colours and brushes for one theme, read once from the Map.* resources in
/// App.axaml and cached per theme variant (the resources never change at run time).
/// </summary>
internal sealed class MapPalette
{
    private static readonly Dictionary<ThemeVariant, MapPalette> Cache = [];

    private readonly Dictionary<MemoryGroup, IImmutableSolidColorBrush> _colour;
    private readonly Dictionary<MemoryGroup, IImmutableSolidColorBrush> _fill;
    private readonly IImmutableSolidColorBrush[] _series;
    private readonly IPen[] _seriesPens;

    private MapPalette(Func<string, Color> get, double tint)
    {
        Surface = new ImmutableSolidColorBrush(get("Map.Surface"));
        Ink = new ImmutableSolidColorBrush(get("Map.Ink"));
        Ink2 = new ImmutableSolidColorBrush(get("Map.Ink2"));
        Border = new ImmutableSolidColorBrush(get("Map.Border"));
        var groups = Enum.GetValues<MemoryGroup>();
        _colour = groups.ToDictionary(g => g, g => (IImmutableSolidColorBrush)new ImmutableSolidColorBrush(get($"Map.{g}")));
        _fill = groups.ToDictionary(g => g, g => (IImmutableSolidColorBrush)new ImmutableSolidColorBrush(Mix(Surface.Color, _colour[g].Color, tint)));
        AvailableOutline = new ImmutablePen(_colour[MemoryGroup.Available], 1, new ImmutableDashStyle([2, 2], 0));
        _series = [.. Enumerable.Range(0, SeriesCount).Select(i => new ImmutableSolidColorBrush(get($"Series.{i}")))];
        _seriesPens = [.. _series.Select(b => new ImmutablePen(b, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round))];
        Grid = new ImmutablePen(new ImmutableSolidColorBrush(get("Map.Grid")), 1);
        Baseline = new ImmutablePen(new ImmutableSolidColorBrush(get("Map.Baseline")), 1);
        Muted = new ImmutableSolidColorBrush(get("Map.Muted"));
        Critical = new ImmutableSolidColorBrush(get("Map.Critical"));
    }

    /// <summary>History chart lines: <see cref="SeriesSlots"/> numbers them.</summary>
    public const int SeriesCount = SeriesSlots.FirstApp + HistoryChart.TopApps;

    public IImmutableSolidColorBrush Muted { get; }

    /// <summary>The status colour for low memory; always shown with words, never alone.</summary>
    public IImmutableSolidColorBrush Critical { get; }

    /// <summary>Horizontal gridlines: hairlines that recede behind the data.</summary>
    public IPen Grid { get; }

    /// <summary>The zero line.</summary>
    public IPen Baseline { get; }

    public IImmutableSolidColorBrush Series(int slot) => _series[slot];

    /// <summary>A history line: 2 px, round joins.</summary>
    public IPen SeriesPen(int slot) => _seriesPens[slot];

    public IImmutableSolidColorBrush Surface { get; }

    public IImmutableSolidColorBrush Ink { get; }

    public IImmutableSolidColorBrush Ink2 { get; }

    public IImmutableSolidColorBrush Border { get; }

    /// <summary>Available memory is drawn as this dashed outline, never filled: it is not in use.</summary>
    public IPen AvailableOutline { get; }

    public static MapPalette For(IResourceHost host, ThemeVariant theme)
    {
        if (Cache.TryGetValue(theme, out var cached))
        {
            return cached;
        }
        Color Get(string key) =>
            host.TryFindResource(key, theme, out var value) && value is Color c
                ? c
                : throw new InvalidOperationException($"Resource '{key}' is missing for theme {theme}.");

        var tint = host.TryFindResource("Map.Tint", theme, out var t) && t is double d ? d : 0.2;
        return Cache[theme] = new MapPalette(Get, tint);
    }

    public IImmutableSolidColorBrush Of(MemoryGroup group) => _colour[group];

    /// <summary>The group colour mixed into the surface: the tile fill, light enough for ink text.</summary>
    public IImmutableSolidColorBrush Fill(MemoryGroup group) => _fill[group];

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + ((b.R - a.R) * t)),
        (byte)Math.Round(a.G + ((b.G - a.G) * t)),
        (byte)Math.Round(a.B + ((b.B - a.B) * t)));
}

/// <summary>A short line in a history series' colour, for the chart legend. Follows theme changes.</summary>
public sealed class SeriesSwatch : Control
{
    public static readonly StyledProperty<int> SlotProperty =
        AvaloniaProperty.Register<SeriesSwatch, int>(nameof(Slot));

    static SeriesSwatch() => AffectsRender<SeriesSwatch>(SlotProperty);

    public SeriesSwatch()
    {
        Width = 14;
        Height = 12;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public int Slot
    {
        get => GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    public override void Render(DrawingContext context) =>
        context.DrawRectangle(MapPalette.For(this, ActualThemeVariant).Series(Slot), null, new Rect(0, (Bounds.Height / 2) - 1.5, Bounds.Width, 3), 1.5, 1.5);
}

/// <summary>A small square in a group's colour, for the legend. Follows theme changes.</summary>
public sealed class GroupSwatch : Control
{
    public static readonly StyledProperty<MemoryGroup> GroupProperty =
        AvaloniaProperty.Register<GroupSwatch, MemoryGroup>(nameof(Group));

    static GroupSwatch() => AffectsRender<GroupSwatch>(GroupProperty);

    public GroupSwatch()
    {
        Width = 12;
        Height = 12;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public MemoryGroup Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var palette = MapPalette.For(this, ActualThemeVariant);
        var rect = new Rect(Bounds.Size);
        if (Group == MemoryGroup.Available)
        {
            context.DrawRectangle(null, palette.AvailableOutline, rect.Deflate(0.5), 3, 3);
            return;
        }
        context.DrawRectangle(palette.Of(Group), null, rect, 3, 3);
    }
}
