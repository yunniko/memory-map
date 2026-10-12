using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using MemoryMap.Windows.Startup;
using Microsoft.Win32;

namespace MemoryMap.Windows.Tests;

/// <summary>
/// A stand-in machine for the startup reader: a scratch registry key under HKCU\Software\
/// MemoryMap.Tests with "user" and "machine" roots laid out like the real hives, and temp
/// folders. Nothing the tests do reaches the real autostart entries. Removed on dispose.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class StartupFixture : IDisposable
{
    public const string Run = WindowsStartupReader.RunKey;
    public const string Run32 = WindowsStartupReader.Run32Key;
    public const string Packages = StoreStartupTasks.AppModel + @"\Repository\Packages";
    public const string TaskStates = StoreStartupTasks.AppModel + @"\SystemAppData";

    public static readonly byte[] On = [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    public static readonly byte[] Off = [3, 0, 0, 0, 0xf5, 0x6b, 0x2d, 0xd0, 0xbd, 0x7e, 0xda, 0x01];

    private FakeLauncher? _launcher;
    private readonly string _keyPath = $@"Software\MemoryMap.Tests\{Guid.NewGuid():N}";

    public StartupFixture()
    {
        Scratch = Registry.CurrentUser.CreateSubKey(_keyPath);
        User = Scratch.CreateSubKey("user");
        Machine = Scratch.CreateSubKey("machine");
        Root = Directory.CreateTempSubdirectory("memorymap-startup-").FullName;
        UserFolder = Directory.CreateDirectory(Path.Combine(Root, "Startup")).FullName;
        MachineFolder = Directory.CreateDirectory(Path.Combine(Root, "Common Startup")).FullName;
        WindowsFolder = Directory.CreateDirectory(Path.Combine(Root, "Windows")).FullName;
        ProgramFiles = Directory.CreateDirectory(Path.Combine(Root, "Program Files")).FullName;
    }

    public RegistryKey Scratch { get; }
    public RegistryKey User { get; }
    public RegistryKey Machine { get; }
    public string Root { get; }
    public string UserFolder { get; }
    public string MachineFolder { get; }
    public string WindowsFolder { get; }
    public string ProgramFiles { get; }

    public StartupLocations Locations => new(User, Machine, UserFolder, MachineFolder, WindowsFolder, [ProgramFiles, Root]);

    public WindowsStartupReader Reader() => new(Locations);

    /// <summary>Stands in for the administrator prompt and Settings; see <see cref="FakeLauncher"/>.</summary>
    public FakeLauncher Launcher => _launcher ??= new(Locations);

    public WindowsStartupActions Actions() => new(Locations, Launcher);

    public static void Set(RegistryKey root, string key, string name, object value)
    {
        using var k = root.CreateSubKey(key);
        k.SetValue(name, value);
    }

    /// <summary>A file standing in for a program (its contents do not matter to the reader).</summary>
    public string Program(params string[] relativePath)
    {
        var path = Path.Combine([ProgramFiles, .. relativePath]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not a real program");
        return path;
    }

    /// <summary>Writes a .lnk shortcut through the Shell, as Explorer would; the target need not exist.</summary>
    public static void Link(string lnkPath, string target, string arguments = "")
    {
        var link = new Shortcut.ShellLink();
        try
        {
            var shell = (Shortcut.IShellLinkW)link;
            shell.SetPath(target);
            shell.SetArguments(arguments);
            ((IPersistFile)link).Save(lnkPath, true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    /// <summary>Registers a package as Windows does: a repository entry naming its folder, which holds the manifest.</summary>
    public string Package(string fullName, string manifest, string? displayName = null)
    {
        var folder = Directory.CreateDirectory(Path.Combine(Root, "WindowsApps", fullName)).FullName;
        File.WriteAllText(Path.Combine(folder, "AppxManifest.xml"), manifest);
        Set(User, $@"{Packages}\{fullName}", "PackageRootFolder", folder);
        if (displayName is not null)
        {
            Set(User, $@"{Packages}\{fullName}", "DisplayName", displayName);
        }
        return folder;
    }

    public static string Manifest(string tasks) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                 xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"
                 xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10">
          <Properties>
            <DisplayName>Fake App</DisplayName>
            <PublisherDisplayName>Fake Publisher</PublisherDisplayName>
          </Properties>
          <Applications>
            <Application Id="App" Executable="app\Fake.exe">
              <Extensions>{tasks}</Extensions>
            </Application>
          </Applications>
        </Package>
        """;

    public void Dispose()
    {
        User.Dispose();
        Machine.Dispose();
        Scratch.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        using (var parent = Registry.CurrentUser.OpenSubKey(@"Software\MemoryMap.Tests", writable: true))
        {
            if (parent is { SubKeyCount: 0 })
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\MemoryMap.Tests", throwOnMissingSubKey: false);
            }
        }
        Directory.Delete(Root, recursive: true);
    }
}

/// <summary>
/// Runs the elevated helper's own code in this process against the fixture (as if the
/// administrator prompt were answered Yes, unless <see cref="Declines"/>), and records the
/// Settings pages opened instead of opening them.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class FakeLauncher(StartupLocations locations) : IStartupLauncher
{
    public bool Declines { get; set; }

    /// <summary>An exit code to report instead of running the helper, as a helper that crashed would.</summary>
    public int? ExitCode { get; set; }

    public List<IReadOnlyList<string>> Elevated { get; } = [];

    public List<string> Opened { get; } = [];

    public int? RunElevated(IReadOnlyList<string> arguments)
    {
        Elevated.Add(arguments);
        return Declines ? null : ExitCode ?? StartupHelper.Execute([.. arguments], locations);
    }

    public string? OpenSettings(string uri)
    {
        Opened.Add(uri);
        return null;
    }
}
