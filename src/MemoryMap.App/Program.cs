using Avalonia;

namespace MemoryMap.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!Environment.Is64BitProcess)
        {
            Console.Error.WriteLine("Memory Map needs 64-bit Windows.");
            return 2;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia previewer. Software rendering: the GPU path cost ~80 MB more at
    // start and grew ~10 MB a minute drawing the chart's changing lines, against criterion 9 (D015).
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
}
