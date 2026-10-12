using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MemoryMap.App;

/// <summary>
/// The low-memory alert: a small topmost window in the corner above the tray that does not take
/// focus. It closes itself after <see cref="Lifetime"/>, or when the user picks a button.
/// </summary>
public partial class AlertWindow : Window
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(20);
    private const int Gap = 12;

    private readonly DispatcherTimer _timer = new() { Interval = Lifetime };

    public AlertWindow()
    {
        InitializeComponent();
        DismissButton.Click += (_, _) => Close();
        ShowButton.Click += (_, _) =>
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
            Close();
        };
        _timer.Tick += (_, _) => Close();
        // Placed again once open, when the real frame size is known.
        Opened += (_, _) =>
        {
            PlaceInCorner();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary>"Show map" was picked.</summary>
    public event EventHandler? ShowRequested;

    public string Text
    {
        get => Message.Text ?? "";
        set
        {
            Message.Text = value;
            // A fresh message gets the full time to be read.
            _timer.Stop();
            if (IsVisible)
            {
                _timer.Start();
            }
        }
    }

    /// <summary>Shows the alert in the corner above the tray.</summary>
    public void ShowInCorner()
    {
        PlaceInCorner();
        Show();
    }

    private void PlaceInCorner()
    {
        if (Screens.Primary is not { } screen)
        {
            return;
        }
        var area = screen.WorkingArea;
        // The frame, not the client area, must fit; before the first layout use the requested size.
        var size = PixelSize.FromSize(FrameSize ?? (Bounds.Size == default ? new Size(Width, Height) : Bounds.Size), screen.Scaling);
        Position = new PixelPoint(area.Right - size.Width - Gap, area.Bottom - size.Height - Gap);
    }
}
