using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

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
                (DataContext as MainViewModel)?.SetHover(Map.HoveredNode);
                PlaceTip(_pointer);
            }
        };
        Map.PointerMoved += (_, e) => PlaceTip(_pointer = e.GetPosition(Map));
    }

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
