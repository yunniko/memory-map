using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MemoryMap.Core;
using MemoryMap.Windows;

namespace MemoryMap.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Only a real desktop run starts sampling; headless tests build their own window.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            WindowsMemorySampler sampler;
            try
            {
                sampler = new WindowsMemorySampler();
            }
            catch (Exception e)
            {
                // No counters, no map, but say why in the window rather than vanish at startup.
                var failed = new MainViewModel();
                failed.ShowFailure(e, DateTimeOffset.Now);
                desktop.MainWindow = new MainWindow { DataContext = failed };
                base.OnFrameworkInitializationCompleted();
                return;
            }
            var live = new LiveMemory(sampler, TimeSpan.FromSeconds(1));
            var model = new MainViewModel(live, new WindowsProcessActions());
            desktop.MainWindow = new MainWindow { DataContext = model };
            desktop.ShutdownRequested += (_, _) =>
            {
                live.Dispose();
                sampler.Dispose();
            };
            live.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
