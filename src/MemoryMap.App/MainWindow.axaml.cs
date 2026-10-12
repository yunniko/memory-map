using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MemoryMap.Core;

namespace MemoryMap.App;

public partial class MainWindow : Window
{
    private const double TipOffset = 16;
    // Where the pointer last was over the map or the chart: only one can be under it at a time.
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
                PlaceTip(Tip, Map, _pointer);
            }
        };
        Map.PointerMoved += (_, e) => PlaceTip(Tip, Map, _pointer = e.GetPosition(Map));
        Chart.PropertyChanged += (_, e) =>
        {
            if (e.Property == HistoryChartControl.HoveredIndexProperty)
            {
                Model?.History.SetHover(Chart.HoveredIndex);
                PlaceTip(ChartTip, Chart, _pointer);
            }
        };
        Chart.PointerMoved += (_, e) => PlaceTip(ChartTip, Chart, _pointer = e.GetPosition(Chart));
        Map.NodeClicked += (_, node) => Model?.ZoomInto(node);
        Map.ContextRequested += OnMapContextRequested;
        // Tunnel, so the mouse back button works wherever the pointer is.
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) =>
        {
            if (Model is { } model)
            {
                model.Confirm = request => new ConfirmDialog(request).ShowDialog<bool>(this);
                model.IsOnScreen = IsOnScreen;
            }
        };
        PropertyChanged += (_, e) =>
        {
            if ((e.Property == IsVisibleProperty || e.Property == WindowStateProperty) && Model is { } model)
            {
                model.IsOnScreen = IsOnScreen;
            }
        };
    }

    private bool IsOnScreen => IsVisible && WindowState != WindowState.Minimized;

    /// <summary>Set when a tray icon exists to bring a hidden window back.</summary>
    public bool CanHideToTray { get; set; }

    /// <summary>Whether a close should hide the window instead: only the user's own close (the
    /// X, Alt+F4), only with somewhere to come back from, and never when Windows is shutting down.</summary>
    internal static bool HidesInstead(WindowCloseReason reason, bool programmatic, bool closeToTray, bool canHide) =>
        canHide && closeToTray && !programmatic && reason == WindowCloseReason.WindowClosing;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel && HidesInstead(e.CloseReason, e.IsProgrammatic, Model?.Settings.CloseToTray == true, CanHideToTray))
        {
            e.Cancel = true;
            Hide();
        }
    }

    /// <summary>Brings the window back from the tray or from behind other windows.</summary>
    public void Reveal()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
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

    // Beside the pointer, flipped to the other side near the right or bottom edge of its host.
    private static void PlaceTip(Control tip, Control host, Point at)
    {
        tip.Measure(Size.Infinity);
        var size = tip.DesiredSize;
        var x = at.X + TipOffset + size.Width > host.Bounds.Width ? at.X - TipOffset - size.Width : at.X + TipOffset;
        var y = at.Y + TipOffset + size.Height > host.Bounds.Height ? at.Y - TipOffset - size.Height : at.Y + TipOffset;
        Canvas.SetLeft(tip, Math.Max(0, x));
        Canvas.SetTop(tip, Math.Max(0, y));
    }
}
