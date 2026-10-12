using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MemoryMap.Core;

namespace MemoryMap.App;

public partial class MainWindow : Window
{
    private const double TipOffset = 16;
    private Point _pointer;

    public MainWindow()
    {
        InitializeComponent();
        // Placed after the tip's content changes, as well as on every move, so its size is current.
        Map.PropertyChanged += (_, e) =>
        {
            if (e.Property == TreemapControl.HoveredNodeProperty)
            {
                Model?.SetHover(Map.HoveredNode);
                PlaceTip(_pointer);
            }
        };
        Map.PointerMoved += (_, e) => PlaceTip(_pointer = e.GetPosition(Map));
        Map.NodeClicked += (_, node) => Model?.ZoomInto(node);
        Map.ContextRequested += OnMapContextRequested;
        // Tunnel, so the mouse back button works wherever the pointer is.
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (Model is { } model)
            {
                model.Confirm = request => new ConfirmDialog(request).ShowDialog<bool>(this);
            }
        };
    }

    /// <summary>The menu a right-click on <paramref name="node"/> opens; null when it has no commands.</summary>
    internal ContextMenu? BuildMenu(MapNode node)
    {
        if (Model is not { } model)
        {
            return null;
        }
        var entries = model.MenuFor(node);
        if (entries.Count == 0)
        {
            return null;
        }
        var items = new List<Control>();
        foreach (var entry in entries)
        {
            if (entry.StartsGroup)
            {
                items.Add(new Separator());
            }
            var item = new MenuItem { Header = entry.Header, IsEnabled = entry.Run is not null };
            if (entry.Run is not null)
            {
                item.Click += async (_, _) => await model.RunAsync(entry);
            }
            items.Add(item);
        }
        return new ContextMenu { ItemsSource = items };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var back = e.Key is Key.Escape or Key.Back || (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt);
        if (!e.Handled && back && GoBack())
        {
            e.Handled = true;
        }
    }

    /// <summary>The context menu opened last, if any.</summary>
    internal ContextMenu? Menu { get; private set; }

    private MainViewModel? Model => DataContext as MainViewModel;

    private void OnMapContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        e.Handled = true;
        // For the node at the click itself; the hovered node stands in for a keyboard request.
        Menu?.Close();
        var node = e.TryGetPosition(Map, out var at) ? Map.Scene.HitTest(at.X, at.Y) : Map.HoveredNode;
        Menu = node is null ? null : BuildMenu(node);
        Menu?.Open(Map);
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsXButton1Pressed && GoBack())
        {
            e.Handled = true;
        }
    }

    // The back gestures (keys, mouse back button) belong to the map; on the table they do nothing.
    private bool GoBack() => Model is { ViewIndex: MainViewModel.MapView } model && model.Back();

    private void OnBack(object? sender, RoutedEventArgs e) => Model?.Back();

    private void OnCrumb(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Crumb crumb })
        {
            Model?.ZoomTo(crumb.Key);
        }
    }

    private void OnDismiss(object? sender, RoutedEventArgs e) => Model?.DismissNotice();

    // Beside the pointer, flipped to the other side near the right or bottom edge.
    private void PlaceTip(Point at)
    {
        Tip.Measure(Size.Infinity);
        var size = Tip.DesiredSize;
        var x = at.X + TipOffset + size.Width > Map.Bounds.Width ? at.X - TipOffset - size.Width : at.X + TipOffset;
        var y = at.Y + TipOffset + size.Height > Map.Bounds.Height ? at.Y - TipOffset - size.Height : at.Y + TipOffset;
        Canvas.SetLeft(Tip, Math.Max(0, x));
        Canvas.SetTop(Tip, Math.Max(0, y));
    }
}
