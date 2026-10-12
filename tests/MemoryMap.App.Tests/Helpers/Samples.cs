using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using MemoryMap.Core;

namespace MemoryMap.App.Tests.Helpers;

/// <summary>Snapshots, an opened window and frame capture shared by the window specs.</summary>
internal static class Samples
{
    public const long Mb = 1024 * 1024;

    public static ProcessSample Proc(int pid, string name, long mb, ProcessKind kind = ProcessKind.App) =>
        new(pid, name + ".exe", kind, kind == ProcessKind.App ? 1 : 0, DateTimeOffset.UnixEpoch.AddSeconds(pid), mb * Mb, mb * Mb * 2, mb * Mb);

    public static MemorySnapshot Snapshot(long chromeMb = 3000, long codeMb = 1500, long availableMb = 4096) => new(
        new DateTimeOffset(2026, 10, 12, 16, 0, 0, TimeSpan.Zero),
        new SystemMemory(16L * 1024 * Mb, availableMb * Mb, 3000 * Mb, 20L * 1024 * Mb, 40L * 1024 * Mb,
            500 * Mb, 400 * Mb, 800 * Mb, 300 * Mb, 20 * Mb, 10 * Mb, 100 * Mb, TimeSpan.FromHours(5)),
        [
            Proc(10, "chrome", chromeMb), Proc(11, "chrome", 400), Proc(12, "code", codeMb), Proc(13, "firefox", 900),
            Proc(20, "svchost", 300, ProcessKind.Service), Proc(21, "MsMpEng", 250, ProcessKind.Service),
            Proc(30, "vmmemWSL", 2500, ProcessKind.VirtualMachine),
        ]);

    public static MemoryBreakdown Breakdown(long chromeMb = 3000, long codeMb = 1500) =>
        Core.Breakdown.Build(Snapshot(chromeMb, codeMb));

    public static (MainWindow Window, MainViewModel Model) Open(MemoryBreakdown? breakdown = null, ThemeVariant? theme = null,
        IProcessActions? actions = null)
    {
        Avalonia.Application.Current!.RequestedThemeVariant = theme ?? ThemeVariant.Light;
        var model = new MainViewModel(actions: actions);
        if (breakdown is not null)
        {
            model.Apply(breakdown);
        }
        var window = new MainWindow { DataContext = model, Width = 1180, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, model);
    }

    /// <summary>Renders the window; also saves it when MEMORYMAP_SCREENSHOTS names a folder, for a person to look at.</summary>
    public static WriteableBitmap Capture(TopLevel window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        var folder = Environment.GetEnvironmentVariable("MEMORYMAP_SCREENSHOTS");
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
            frame.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
        }
        return frame;
    }

    public static Avalonia.Media.Color PixelAt(WriteableBitmap bitmap, int x, int y)
    {
        using var buffer = bitmap.Lock();
        var raw = (uint)System.Runtime.InteropServices.Marshal.ReadInt32(buffer.Address, (y * buffer.RowBytes) + (x * 4));
        byte Byte(int i) => (byte)(raw >> (8 * i));
        // Little-endian: byte 0 is the first channel of the format.
        return buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
            ? Avalonia.Media.Color.FromArgb(Byte(3), Byte(2), Byte(1), Byte(0))
            : Avalonia.Media.Color.FromArgb(Byte(3), Byte(0), Byte(1), Byte(2));
    }
}
