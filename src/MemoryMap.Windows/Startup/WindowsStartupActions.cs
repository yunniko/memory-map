using System.ComponentModel;
using System.Runtime.Versioning;
using MemoryMap.Core;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// Switches startup entries on and off by writing their StartupApproved flag, as Windows' own
/// switch does, and nothing else: the registration itself is never touched, so a switch is
/// always reversible (D021). This user's entries are written here. A machine-wide entry is
/// written by this program run again as administrator for that one change (<see cref="StartupHelper"/>,
/// D024); a Store app's switch opens Windows Settings, the only documented way to change it.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStartupActions(StartupLocations locations, IStartupLauncher launcher) : IStartupActions
{
    internal const string SettingsPage = "ms-settings:startupapps";

    public WindowsStartupActions()
        : this(StartupLocations.ForCurrentUser(), new ShellStartupLauncher())
    {
    }

    public ActionResult SetEnabled(StartupEntry entry, bool enabled) => entry.Switch switch
    {
        StartupSwitch.Direct => Direct(entry, enabled),
        StartupSwitch.NeedsAdmin => Elevated(entry, enabled),
        _ => launcher.OpenSettings(SettingsPage) is { } error
            ? StartupResult.Failed(entry, "open Windows Settings for", error)
            : StartupResult.InSettings(entry),
    };

    public ActionResult ShowInExplorer(StartupEntry entry)
    {
        if (FileToShow(entry) is not { } path)
        {
            return StartupResult.Failed(entry, "show", entry.Problem ?? "it names no program file.");
        }
        return Explorer.Select(path) is { } error ? StartupResult.Failed(entry, "show", error) : ActionResult.Shown(path);
    }

    /// <summary>The program file when it exists; else a Startup-folder entry's own file; else null.</summary>
    internal string? FileToShow(StartupEntry entry)
    {
        if (entry.ExecutablePath is { } exe && File.Exists(exe))
        {
            return exe;
        }
        if (entry.Source != StartupSource.StartupFolder)
        {
            return null;
        }
        var file = StartupIds.Parse(entry.Id).FolderFile(locations);
        return File.Exists(file) ? file : null;
    }

    private ActionResult Direct(StartupEntry entry, bool enabled)
    {
        StartupTarget target;
        try
        {
            target = StartupIds.Parse(entry.Id);
        }
        catch (ArgumentException e)
        {
            return StartupResult.Failed(entry, "switch", e.Message);
        }
        var (outcome, error) = StartupFlags.Write(locations, target, enabled);
        return Report(entry, enabled, outcome, error);
    }

    private ActionResult Elevated(StartupEntry entry, bool enabled)
    {
        int? exit;
        try
        {
            exit = launcher.RunElevated([StartupHelper.Verb, entry.Id, enabled ? "on" : "off"]);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return StartupResult.Failed(entry, "switch", $"the administrator step did not start ({e.Message}).");
        }
        if (exit is not { } code)
        {
            return StartupResult.Declined(entry);
        }
        // The helper reports only its exit code; a code it never returns is a failure too.
        var outcome = Enum.IsDefined((FlagWrite)code) ? (FlagWrite)code : FlagWrite.Failed;
        return Report(entry, enabled, outcome, $"the administrator step ended with code {code}.");
    }

    private static ActionResult Report(StartupEntry entry, bool enabled, FlagWrite outcome, string? error) => outcome switch
    {
        FlagWrite.Switched => StartupResult.Switched(entry, enabled),
        FlagWrite.Gone => StartupResult.Gone(entry),
        FlagWrite.NotKept => StartupResult.Failed(entry, "switch", "Windows did not keep the change."),
        _ => StartupResult.Failed(entry, "switch", error ?? "the change was refused."),
    };
}
