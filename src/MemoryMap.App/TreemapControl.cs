using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>
/// Draws a <see cref="MapNode"/> as a squarified treemap: its children as framed groups and
/// their children as tiles (layout and stable order come from <see cref="SceneArranger"/>).
/// With <see cref="Titles"/> off a group has no title strip, only a thin outline.
/// Reports the node under the pointer through <see cref="HoveredNode"/>, and a left click
/// (pressed and released over the same node) through <see cref="NodeClicked"/>.
/// </summary>
public sealed class TreemapControl : Control
{
    public static readonly StyledProperty<MapNode?> RootProperty =
        AvaloniaProperty.Register<TreemapControl, MapNode?>(nameof(Root));

    /// <summary>Total RAM, so tiles can show their share of it.</summary>
    public static readonly StyledProperty<long> TotalProperty =
        AvaloniaProperty.Register<TreemapControl, long>(nameof(Total));

    /// <summary>Whether each group shows a title strip.</summary>
    public static readonly StyledProperty<bool> TitlesProperty =
        AvaloniaProperty.Register<TreemapControl, bool>(nameof(Titles), defaultValue: true);

    public static readonly DirectProperty<TreemapControl, MapNode?> HoveredNodeProperty =
        AvaloniaProperty.RegisterDirect<TreemapControl, MapNode?>(nameof(HoveredNode), c => c.HoveredNode);

    private const double Radius = 4;
    private const double Bar = 3;
    private static readonly Typeface Bold = new(FontFamily.Default, weight: FontWeight.SemiBold);
    private static readonly Typeface Regular = new(FontFamily.Default);

    private readonly SceneArranger _arranger = new();
    private Scene _scene = Scene.Empty;
    private Size _arrangedSize;
    private bool _arrangedTitles = true;
    private MapNode? _hovered;
    private Point? _pointer;
    private string? _pressedKey;

    static TreemapControl() => AffectsRender<TreemapControl>(RootProperty, TotalProperty, TitlesProperty);

    public TreemapControl()
    {
        ClipToBounds = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public MapNode? Root
    {
        get => GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    public long Total
    {
        get => GetValue(TotalProperty);
        set => SetValue(TotalProperty, value);
    }

    public bool Titles
    {
        get => GetValue(TitlesProperty);
        set => SetValue(TitlesProperty, value);
    }

    public MapNode? HoveredNode
    {
        get => _hovered;
        private set => SetAndRaise(HoveredNodeProperty, ref _hovered, value);
    }

    public event EventHandler<MapNode>? NodeClicked;

    /// <summary>The scene last drawn; the layout tests and hit-testing read it.</summary>
    public Scene Scene => Arrange();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RootProperty || change.Property == TitlesProperty)
        {
            // A refresh or a new layout moves tiles under a still pointer: hover whatever is there now.
            UpdateHover();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointer = e.GetPosition(this);
        UpdateHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        _pressedKey = point.Properties.IsLeftButtonPressed ? Arrange().HitTest(point.Position.X, point.Position.Y)?.Key : null;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var (pressed, at) = (_pressedKey, e.GetPosition(this));
        _pressedKey = null;
        if (e.InitialPressMouseButton == MouseButton.Left && Arrange().HitTest(at.X, at.Y) is { } node && node.Key == pressed)
        {
            NodeClicked?.Invoke(this, node);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        UpdateHover();
    }

    // Re-raised with the fresh node even when the key is unchanged, so the tooltip shows current numbers.
    private void UpdateHover()
    {
        var hit = _pointer is { } p ? Arrange().HitTest(p.X, p.Y) : null;
        if (hit?.Key != _hovered?.Key)
        {
            InvalidateVisual();
        }
        HoveredNode = hit;
    }

    public override void Render(DrawingContext context)
    {
        var palette = MapPalette.For(this, ActualThemeVariant);
        var scene = Arrange();
        var (ink, ink2) = (palette.Ink, palette.Ink2);
        context.DrawRectangle(palette.Surface, new Pen(palette.Border), new Rect(Bounds.Size).Deflate(0.5), 8, 8);

        var outline = new Pen(palette.Border);
        foreach (var frame in scene.Frames)
        {
            if (!Titles)
            {
                context.DrawRectangle(null, outline, ToRect(frame.Rect).Deflate(0.5), Radius, Radius);
            }
            if (frame.Node.Key == _hovered?.Key)
            {
                context.DrawRectangle(null, new Pen(ink, 2), ToRect(frame.Rect).Deflate(1), Radius, Radius);
            }
            if (frame.Header.Height > 0)
            {
                // Drop the share, then the size, before cutting a number in half.
                var n = frame.Node;
                DrawLabel(context, Bold, 12, ink2, ToRect(frame.Header).Deflate(new Thickness(4, 2)),
                    $"{n.Name}  {Format.SizeShare(n.Bytes, Total)}", $"{n.Name}  {Format.Bytes(n.Bytes)}", n.Name);
            }
        }

        foreach (var tile in scene.Tiles)
        {
            var rect = ToRect(tile.Rect);
            if (rect.Width < 1 || rect.Height < 1)
            {
                continue;
            }
            var group = tile.Node.Group;
            if (group == MemoryGroup.Available)
            {
                context.DrawRectangle(null, palette.AvailableOutline, rect.Deflate(0.5), Radius, Radius);
            }
            else
            {
                context.DrawRectangle(palette.Fill(group), null, rect, Radius, Radius);
                using (context.PushClip(new RoundedRect(rect, Radius)))
                {
                    context.FillRectangle(palette.Of(group), rect.WithWidth(Math.Min(Bar, rect.Width)));
                }
            }

            if (rect.Width >= 36 && rect.Height >= 18)
            {
                var inner = rect.Deflate(new Thickness(Bar + 4, 3, 4, 2));
                DrawLabel(context, Bold, 12, ink, inner, tile.Node.Name);
                if (rect.Height >= 34)
                {
                    var b = tile.Node.Bytes;
                    DrawLabel(context, Regular, 11, ink2, inner.Deflate(new Thickness(0, 16, 0, 0)), Format.SizeShare(b, Total), Format.Bytes(b));
                }
            }

            if (tile.Node.Key == _hovered?.Key)
            {
                context.DrawRectangle(null, new Pen(ink, 2), rect.Deflate(1), Radius, Radius);
            }
        }
    }

    private Scene Arrange()
    {
        var root = Root;
        if (root is null)
        {
            return Scene.Empty;
        }
        if (!ReferenceEquals(root, _scene.Root) || Bounds.Size != _arrangedSize || Titles != _arrangedTitles)
        {
            _scene = _arranger.Arrange(root, new LayoutRect(0, 0, Bounds.Width, Bounds.Height).Inset(4), Titles);
            (_arrangedSize, _arrangedTitles) = (Bounds.Size, Titles);
        }
        return _scene;
    }

    /// <summary>
    /// Draws the first of <paramref name="texts"/> (longest first) that fits whole; only the last
    /// one is ever trimmed with an ellipsis.
    /// </summary>
    private static void DrawLabel(DrawingContext context, Typeface face, double size, IBrush brush, Rect area, params string[] texts)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }
        FormattedText? formatted = null;
        foreach (var text in texts)
        {
            formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush);
            if (formatted.Width <= area.Width)
            {
                break;
            }
        }
        if (formatted!.Width > area.Width)
        {
            formatted.MaxTextWidth = area.Width;
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }
        if (formatted.Height > area.Height + 1)
        {
            return;
        }
        using (context.PushClip(area))
        {
            context.DrawText(formatted, area.TopLeft);
        }
    }

    private static Rect ToRect(LayoutRect r) => new(r.X, r.Y, r.Width, r.Height);
}
