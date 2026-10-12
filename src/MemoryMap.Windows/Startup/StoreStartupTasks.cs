using System.Runtime.Versioning;
using System.Xml;
using System.Xml.Linq;
using MemoryMap.Core;
using Microsoft.Win32;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// Store apps' startup tasks: each installed package's manifest declares them, and the user's
/// on/off state for each sits in the AppModel registry. Both locations are undocumented but
/// have been stable since Windows 10; see D019.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StoreStartupTasks
{
    public const string AppModel = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel";
    private const string StartupCategory = "windows.startupTask";

    public static IEnumerable<StartupEntry> Read(RegistryKey user, List<string> unreadable)
    {
        using var packages = user.OpenSubKey(AppModel + @"\Repository\Packages");
        using var states = user.OpenSubKey(AppModel + @"\SystemAppData");
        if (packages is null)
        {
            // No Store apps registered for this user (or an older Windows): nothing to list.
            return [];
        }
        var entries = new List<StartupEntry>();
        foreach (var package in LatestPerFamily(packages))
        {
            try
            {
                entries.AddRange(TasksOf(package, states));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException or ArgumentException or NotSupportedException)
            {
                unreadable.Add($"Store app {package.FullName}: {e.Message}");
            }
        }
        return entries;
    }

    private sealed record Package(string FullName, string Name, string Family, string Root, string? DisplayName);

    // A package can be registered in several versions at once while it updates; the newest
    // whose folder exists runs. Versions are compared by name first, so only the chosen
    // version's key is opened in the usual case.
    private static IEnumerable<Package> LatestPerFamily(RegistryKey packages)
    {
        var versions = new List<(string FullName, string Name, string Family, Version Version)>();
        foreach (var fullName in packages.GetSubKeyNames())
        {
            // Name_Version_Architecture_ResourceId_PublisherId. Resource packages (a ResourceId such
            // as "split.scale-125") share the app's family but hold no manifest of their own tasks.
            var parts = fullName.Split('_');
            if (parts.Length == 5 && parts[3].Length == 0 && Version.TryParse(parts[1], out var version))
            {
                versions.Add((fullName, parts[0], $"{parts[0]}_{parts[4]}", version));
            }
        }
        foreach (var family in versions.GroupBy(v => v.Family, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var v in family.OrderByDescending(v => v.Version))
            {
                using var key = packages.OpenSubKey(v.FullName);
                if (key?.GetValue("PackageRootFolder") is string root && Directory.Exists(root))
                {
                    yield return new Package(v.FullName, v.Name, v.Family, root, key.GetValue("DisplayName") as string);
                    break;
                }
            }
        }
    }
    private static IEnumerable<StartupEntry> TasksOf(Package package, RegistryKey? states)
    {
        // A sparse package (Copilot here) keeps its files at an outside location and its
        // manifest where Windows installed it; Windows reports that place for the full name.
        var manifestPath = new[] { package.Root, InstalledPath(package.FullName) }
            .Where(folder => folder is not null)
            .Select(folder => Path.Combine(folder!, "AppxManifest.xml"))
            .FirstOrDefault(File.Exists);
        if (manifestPath is null)
        {
            yield break;
        }
        var text = File.ReadAllText(manifestPath);
        // Most packages declare no startup task: skip parsing them.
        if (!text.Contains(StartupCategory, StringComparison.Ordinal))
        {
            yield break;
        }
        var manifest = XDocument.Parse(text);
        var properties = manifest.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Properties");
        var publisher = Resolve(package, Child(properties, "PublisherDisplayName"));
        var packageName = Resolve(package, package.DisplayName) ?? Resolve(package, Child(properties, "DisplayName"));

        foreach (var extension in manifest.Descendants().Where(e =>
            e.Name.LocalName == "Extension" && (string?)e.Attribute("Category") == StartupCategory))
        {
            var task = extension.Elements().FirstOrDefault(e => e.Name.LocalName == "StartupTask");
            if (task?.Attribute("TaskId")?.Value is not { Length: > 0 } taskId)
            {
                continue;
            }
            var application = extension.Ancestors().FirstOrDefault(e => e.Name.LocalName == "Application");
            var executable = (string?)extension.Attribute("Executable") ?? (string?)application?.Attribute("Executable");
            var exePath = executable is null ? null : Path.GetFullPath(Path.Combine(package.Root, executable.Replace('/', '\\')));
            var name = Resolve(package, (string?)task.Attribute("DisplayName")) ?? packageName ?? package.Name;
            var declaredOn = string.Equals((string?)task.Attribute("Enabled"), "true", StringComparison.OrdinalIgnoreCase);

            using var stateKey = states?.OpenSubKey($@"{package.Family}\{taskId}");
            yield return new StartupEntry(
                Id: $"task/{package.Family}/{taskId}",
                Name: name,
                Publisher: publisher,
                Command: exePath ?? package.Root,
                Location: $"Store app {package.Family}",
                Source: StartupSource.StoreTask,
                Scope: StartupScope.User,
                Enabled: IsEnabled(stateKey?.GetValue("State"), declaredOn),
                ExecutablePath: exePath,
                ProgramFolder: package.Root,
                Problem: exePath is not null && !File.Exists(exePath) ? WindowsStartupReader.Missing(exePath) : null);
        }
    }

    /// <summary>
    /// The StartupTaskState the user's registry holds: Enabled (2) and EnabledByPolicy (4) are on;
    /// Disabled (0), DisabledByUser (1) and DisabledByPolicy (3) are off. With no state yet, the
    /// manifest's declared default applies.
    /// </summary>
    internal static bool IsEnabled(object? state, bool declaredOn) => state is int value ? value is 2 or 4 : declaredOn;

    private static string? Child(XElement? parent, string name) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    // "ms-resource:AppName" and "@{...}" strings are looked up in the package's resources; a
    // plain string is used as is. Null when it cannot be resolved, so the caller falls back.
    private static string? Resolve(Package package, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (text.StartsWith('@'))
        {
            return Indirect(text);
        }
        if (!text.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
        {
            return text.Trim();
        }
        var id = text["ms-resource:".Length..];
        string[] uris = id.StartsWith("//", StringComparison.Ordinal) ? [$"ms-resource:{id}"]
            : id.StartsWith('/') ? [$"ms-resource://{package.Name}{id}"]
            : [$"ms-resource://{package.Name}/Resources/{id}", $"ms-resource://{package.Name}/{id}"];
        return uris.Select(uri => Indirect($"@{{{package.FullName}?{uri}}}")).FirstOrDefault(s => s is not null);
    }

    // Room for any package path or resource string Windows returns here.
    private const int BufferSize = 1024;

    private static unsafe string? InstalledPath(string fullName)
    {
        var length = (uint)BufferSize;
        var buffer = stackalloc char[BufferSize];
        return Native.GetPackagePathByFullName(fullName, ref length, buffer) == 0 ? new string(buffer) : null;
    }

    private static unsafe string? Indirect(string source)
    {
        var buffer = stackalloc char[BufferSize];
        return Native.SHLoadIndirectString(source, buffer, BufferSize, 0) == 0 && new string(buffer) is { Length: > 0 } s ? s : null;
    }
}
