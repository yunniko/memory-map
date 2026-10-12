using Avalonia;
using Avalonia.Headless;
using MemoryMap.App.Tests.Helpers;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace MemoryMap.App.Tests.Helpers;

/// <summary>The real App (theme, resources) on the headless platform, with Skia so frames can be captured.</summary>
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
