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

    // Also used by the Avalonia previewer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
