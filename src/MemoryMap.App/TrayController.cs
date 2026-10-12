using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MemoryMap.Core;

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

    private readonly MainViewModel _model;
    private readonly MapPalette _palette;
    private readonly Action _show;
    private readonly TrayIcon _icon;
    private (int Level, bool Low)? _drawn;

    public TrayController(Application app, MainViewModel model, Action show, Action exit)
    {
        _model = model;
        _show = show;
        // The taskbar is dark in both Windows themes the app is likely to sit beside: dark colours.
        _palette = MapPalette.For(app, ThemeVariant.Dark);
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

    /// <summary>Draws the gauge: <paramref name="percent"/> of the body filled from the bottom,
    /// in the first history colour (In use), or critical red while memory is low.</summary>
    internal static RenderTargetBitmap Render(MapPalette palette, int percent, bool low, int size = 32)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        using var context = bitmap.CreateDrawingContext();
        var body = new Rect(0, 0, size, size);
        context.DrawRectangle(palette.Baseline.Brush, null, body, size / 6.0, size / 6.0);
        var inner = body.Deflate(size / 8.0);
        var height = inner.Height * Math.Clamp(percent, 0, 100) / 100;
        context.DrawRectangle(low ? palette.Critical : palette.Series(SeriesSlots.Used), null, new Rect(inner.X, inner.Bottom - height, inner.Width, height), 1.5, 1.5);
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
        // WindowIcon copies the pixels into its own PNG stream, so the bitmap can go at once.
        using var bitmap = Render(_palette, state.Item1, state.IsLowMemory);
        _icon.Icon = new WindowIcon(bitmap);
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
        alert.ShowInCorner();
    }
}
