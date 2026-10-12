using Avalonia;
using MemoryMap.Windows.Startup;

namespace MemoryMap.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Run again elevated for one machine-wide startup change: write it and exit, no window (D024).
        if (OperatingSystem.IsWindows() && StartupHelper.Run(args) is { } helperExit)
        {
            return helperExit;
        }
        if (!Environment.Is64BitProcess)
        {
            Console.Error.WriteLine("Memory Map needs a 64-bit system.");
            return 2;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia previewer. Software rendering: the GPU path cost ~80 MB more at
    // start and grew ~10 MB a minute drawing the chart's changing lines, against criterion 9 (D015).
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] })
        .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
}
