using MemoryMap.Core;
using MemoryMap.Linux;
using MemoryMap.Windows;
using MemoryMap.Windows.Startup;

namespace MemoryMap.App;

/// <summary>
/// This OS's implementations of what the app needs from it, picked once at startup; the rest of
/// the app sees only the Core interfaces.
/// </summary>
internal sealed record PlatformServices(IMemorySampler Sampler, IProcessActions Actions, StartupViewModel Startup, TrayHost Tray)
{
    /// <exception cref="PlatformNotSupportedException">Neither Windows nor Linux.</exception>
    public static PlatformServices ForThisOs()
    {
        if (OperatingSystem.IsWindows())
        {
            return new(new WindowsMemorySampler(), new WindowsProcessActions(),
                new StartupViewModel(new WindowsStartupReader(), new WindowsStartupActions(), new WindowsProcessPaths()), TrayHost.Always());
        }
        if (OperatingSystem.IsLinux())
        {
            return new(new LinuxMemorySampler(), new LinuxProcessActions(), new StartupViewModel(), TrayHost.Probed(Desktop.HasTrayHost));
        }
        throw new PlatformNotSupportedException("Memory Map runs on Windows and Linux.");
    }
}
