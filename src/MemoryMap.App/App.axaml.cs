using Avalonia;
using Avalonia.Controls;
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
            var (stored, problem) = SettingsStore.Load(SettingsViewModel.DefaultPath);
            var settings = new SettingsViewModel(stored, SettingsViewModel.DefaultPath, problem);
            var live = new LiveMemory(sampler, TimeSpan.FromSeconds(stored.RefreshSeconds));
            var model = new MainViewModel(live, new WindowsProcessActions(), settings);
            var window = new MainWindow { DataContext = model, CanHideToTray = true };
            desktop.MainWindow = window;
            // Closing the window may only hide it; the app ends when it really closes (Exit, or tray off).
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var tray = new TrayController(this, model, window.Reveal, () => desktop.Shutdown());
            desktop.ShutdownRequested += (_, _) =>
            {
                tray.Dispose();
                live.Dispose();
                sampler.Dispose();
            };
            live.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
