using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MemoryMap.Core;

namespace MemoryMap.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Only a real desktop run starts sampling; headless tests build their own window.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            PlatformServices os;
            try
            {
                os = PlatformServices.ForThisOs();
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
            var live = new LiveMemory(os.Sampler, TimeSpan.FromSeconds(stored.RefreshSeconds));
            var model = new MainViewModel(live, os.Actions, settings, os.Startup, os.Tray);
            var window = new MainWindow { DataContext = model };
            desktop.MainWindow = window;
            // Closing the window may only hide it; the app ends when it really closes (Exit, or tray off).
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var tray = new TrayController(this, model, window.Reveal, () => desktop.Shutdown());
            desktop.ShutdownRequested += (_, _) =>
            {
                tray.Dispose();
                live.Dispose();
                os.Sampler.Dispose();
            };
            // For the Settings tab; each close asks again (D031).
            _ = os.Tray.RefreshAsync();
            live.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
