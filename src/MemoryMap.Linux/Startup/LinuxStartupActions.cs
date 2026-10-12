using System.Security.Cryptography;
using System.Text;
using MemoryMap.Core;

namespace MemoryMap.Linux.Startup;

/// <summary>
/// Switches XDG autostart entries for this user only, the way the session reads them: the
/// user's own file gets Hidden=true or false; an entry for every user gets a copy in the
/// user's autostart folder that overrides it. System files are never written, and the only
/// file ever removed is such a copy the app wrote itself and nobody edited since (D032).
/// systemd user services are listed only (D033).
/// </summary>
public sealed class LinuxStartupActions : IStartupActions
{
    /// <summary>Marks a copy the app wrote, so it alone may be removed again.</summary>
    internal const string OverrideKey = "X-MemoryMap-Override";

    private readonly LinuxStartupLocations _locations;
    private readonly LinuxStartupReader _reader;
    private readonly Func<string, string?> _showFile;

    public LinuxStartupActions()
        : this(LinuxStartupLocations.ForCurrentUser())
    {
    }

    internal LinuxStartupActions(LinuxStartupLocations locations, Func<string, string?>? showFile = null)
    {
        _locations = locations;
        _reader = new LinuxStartupReader(locations, _ => null); // reading back needs no systemctl
        _showFile = showFile ?? Desktop.ShowFile;
    }

    public ActionResult SetEnabled(StartupEntry entry, bool enabled)
    {
        if (entry.Source == StartupSource.SystemdUserService)
        {
            return StartupResult.ReadOnly(entry);
        }
        if (FileName(entry) is not { } name)
        {
            return StartupResult.Failed(entry, "switch", "it is not a desktop autostart entry.");
        }
        try
        {
            var files = _reader.Files(name);
            if (_reader.Autostart(files) is not { } current)
            {
                return StartupResult.Gone(entry);
            }
            if (current.Enabled != enabled)
            {
                Write(files, enabled);
            }
            return _reader.ReadAutostart(name) switch
            {
                null => StartupResult.Gone(entry),
                { Enabled: var now } when now == enabled => StartupResult.Switched(entry, enabled),
                _ => StartupResult.Failed(entry, "switch", "the change did not hold: the file was changed meanwhile."),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return StartupResult.Failed(entry, "switch", e.Message);
        }
    }

    public ActionResult ShowFileLocation(StartupEntry entry)
    {
        var path = entry.ExecutablePath is { } exe && File.Exists(exe) ? exe
            : FileName(entry) is { } name ? _reader.Files(name).Effective
            : null;
        if (path is null)
        {
            return StartupResult.Failed(entry, "show", entry.Problem ?? "it names no program file.");
        }
        return _showFile(path) is { } error ? StartupResult.Failed(entry, "show", error) : ActionResult.Shown(path);
    }

    private static string? FileName(StartupEntry entry) =>
        entry.Source == StartupSource.XdgAutostart && entry.Id.StartsWith(LinuxStartupReader.AutostartPrefix, StringComparison.Ordinal)
            ? entry.Id[LinuxStartupReader.AutostartPrefix.Length..]
            : null;

    private void Write(AutostartFiles files, bool enabled)
    {
        if (files.System is not { } system)
        {
            Save(files.User!, Switched(File.ReadAllText(files.User!), enabled)); // the user's own entry
            return;
        }
        var systemText = File.ReadAllText(system);
        if (files.User is not { } copy)
        {
            Save(Path.Combine(_locations.UserFolder, files.Name), Marked(Switched(systemText, enabled)));
            return;
        }
        var text = File.ReadAllText(copy);
        if (IsUntouchedOwnCopy(text))
        {
            if (enabled && Parse(systemText).StartsAtLogin)
            {
                File.Delete(copy); // without it the system entry applies, and that starts by itself
                return;
            }
            Save(copy, Marked(Switched(text, enabled)));
            return;
        }
        // Anyone else's copy, or one edited since, is switched in place. A bare override (Hidden=true
        // alone) has no command to start: it gets the system entry's keys it lacks, and keeps its own.
        Save(copy, Switched(Parse(text)["Exec"] is null ? Completed(text, systemText) : text, enabled));
    }

    private static string Switched(string text, bool enabled)
    {
        var entry = Parse(text);
        text = entry.With("Hidden", enabled ? "false" : "true");
        // GNOME's own off switch; Hidden alone would leave the entry off.
        return enabled && entry["X-GNOME-Autostart-enabled"] == "false" ? Parse(text).With("X-GNOME-Autostart-enabled", "true") : text;
    }

    private static string Completed(string text, string systemText)
    {
        var own = Parse(text);
        foreach (var (key, value) in Parse(systemText).RawKeys.Where(k => own[k.Key] is null))
        {
            text = Parse(text).With(key, value);
        }
        return text;
    }

    // The mark carries a checksum of the copy as written: an edit since (even one that keeps the
    // mark) makes it someone else's file, which is never removed (D032).
    private static string Marked(string text)
    {
        var unsigned = Parse(text).With(OverrideKey, "");
        return Parse(unsigned).With(OverrideKey, Checksum(unsigned));
    }

    internal static bool IsUntouchedOwnCopy(string text) =>
        Parse(text) is var entry && entry[OverrideKey] is { Length: > 0 } mark && mark == Checksum(entry.With(OverrideKey, ""));

    private static string Checksum(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    private static DesktopEntry Parse(string text) =>
        DesktopEntry.Parse(text) ?? throw new InvalidDataException("not a desktop entry (no [Desktop Entry] group).");

    // Written beside the file and renamed over it, so the session never reads half a file. A link
    // (a dotfiles repo) is written through, and the file keeps its permissions.
    private static void Save(string path, string text)
    {
        var target = File.Exists(path) && Libc.RealPath(path) is { } real ? real : path;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".memory-map-tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows() && File.Exists(target))
        {
            File.SetUnixFileMode(temp, File.GetUnixFileMode(target));
        }
        File.Move(temp, target, overwrite: true);
    }
}
