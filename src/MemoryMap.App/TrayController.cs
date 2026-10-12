using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;

namespace MemoryMap.App;

/// <summary>
/// The tray icon and the low-memory alert window. The icon is a gauge filled to the share of RAM
/// in use, red while memory is low; its tooltip carries the headline figures. Clicking it, or
/// "Show map" on the alert, brings the window back; Exit on its menu is the way to quit while
/// closing the window only hides it.
/// </summary>
internal sealed class TrayController : IDisposable
{
    /// <summary>The gauge moves in steps of this many percent, so the icon is redrawn only on a visible change.</summary>
    public const int Step = 5;

    private static readonly IImmutableSolidColorBrush Body = new ImmutableSolidColorBrush(Color.Parse("#383835"));
    private static readonly IImmutableSolidColorBrush Level = new ImmutableSolidColorBrush(Color.Parse("#3987e5"));
    private static readonly IImmutableSolidColorBrush Critical = new ImmutableSolidColorBrush(Color.Parse("#d03b3b"));

    private readonly MainViewModel _model;
    private readonly Action _show;
    private readonly TrayIcon _icon;
    private (int Level, bool Low)? _drawn;

    public TrayController(Application app, MainViewModel model, Action show, Action exit)
    {
        _model = model;
        _show = show;
        var showItem = new NativeMenuItem("Show Memory Map");
        showItem.Click += (_, _) => show();
        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) => exit();
        _icon = new TrayIcon { ToolTipText = model.TrayTip, Menu = [showItem, new NativeMenuItemSeparator(), exitItem] };
        _icon.Clicked += (_, _) => show();
        TrayIcon.SetIcons(app, [_icon]);
        UpdateIcon();
        model.PropertyChanged += OnModelChanged;
    }

    /// <summary>The alert on screen, if any.</summary>
    public AlertWindow? Alert { get; private set; }

    /// <summary>Draws the gauge: <paramref name="percent"/> of the body filled from the bottom.</summary>
    public static RenderTargetBitmap Render(int percent, bool low, int size = 32)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        using var context = bitmap.CreateDrawingContext();
        var body = new Rect(0, 0, size, size);
        context.DrawRectangle(Body, null, body, size / 6.0, size / 6.0);
        var inner = body.Deflate(size / 8.0);
        var height = inner.Height * Math.Clamp(percent, 0, 100) / 100;
        context.DrawRectangle(low ? Critical : Level, null, new Rect(inner.X, inner.Bottom - height, inner.Width, height), 1.5, 1.5);
        return bitmap;
    }

    public void Dispose()
    {
        _model.PropertyChanged -= OnModelChanged;
        Alert?.Close();
        _icon.Dispose();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.TrayTip):
                _icon.ToolTipText = _model.TrayTip;
                break;
            case nameof(MainViewModel.UsedPercent):
                UpdateIcon();
                break;
            case nameof(MainViewModel.IsLowMemory):
                UpdateIcon();
                if (_model.IsLowMemory)
                {
                    ShowAlert(_model.AlertMessage);
                }
                else
                {
                    // Memory recovered: the warning no longer holds.
                    Alert?.Close();
                }
                break;
        }
    }

    private void UpdateIcon()
    {
        var state = ((int)Math.Round(_model.UsedPercent / (double)Step) * Step, _model.IsLowMemory);
        if (state == _drawn)
        {
            return;
        }
        _drawn = state;
        // Not disposed here: the platform may read the bitmap after this returns.
        _icon.Icon = new WindowIcon(Render(state.Item1, state.IsLowMemory));
    }

    // One alert at a time: a new warning replaces the text of one still on screen.
    private void ShowAlert(string message)
    {
        if (Alert is { } open)
        {
            open.Text = message;
            return;
        }
        var alert = new AlertWindow { Text = message };
        alert.ShowRequested += (_, _) => _show();
        alert.Closed += (_, _) => Alert = null;
        Alert = alert;
        alert.Show();
    }
}
