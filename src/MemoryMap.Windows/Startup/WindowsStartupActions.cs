using System.Runtime.Versioning;
using System.Security;
using MemoryMap.Core;
using Microsoft.Win32;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// Switches this user's Run values and Startup-folder files on and off by writing their
/// StartupApproved flag, as Windows' own switch does, and nothing else: the registration itself
/// is never touched, so a switch is always reversible. Machine-wide entries need an
/// administrator and Store tasks belong to Settings; both are refused here. See D021.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStartupActions(StartupLocations locations) : IStartupActions
{
    public WindowsStartupActions()
        : this(StartupLocations.ForCurrentUser())
    {
    }

    public ActionResult SetEnabled(StartupEntry entry, bool enabled)
    {
        if (entry.Switch != StartupSwitch.Direct)
        {
            return StartupResult.NotSwitchable(entry);
        }
        try
        {
            var target = StartupIds.Parse(entry.Id);
            // A flag for something no longer registered would only be clutter; say so instead.
            if (!IsRegistered(target))
            {
                return StartupResult.Gone(entry);
            }
            using var approval = locations.User.CreateSubKey(target.ApprovalPath, writable: true);
            approval.SetValue(target.Name, StartupApproval.Flag(enabled, DateTimeOffset.Now), RegistryValueKind.Binary);
            return StartupApproval.IsEnabled(approval.GetValue(target.Name)) == enabled
                ? StartupResult.Switched(entry, enabled)
                : StartupResult.Failed(entry, "switch", "Windows did not keep the change.");
        }
        // ArgumentException: an id this switch does not know, or a name the registry refuses.
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            return StartupResult.Failed(entry, "switch", e.Message);
        }
    }

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
        var file = FolderFile(StartupIds.Parse(entry.Id));
        return File.Exists(file) ? file : null;
    }

    private string FolderFile(StartupTarget target) =>
        Path.Combine(target.Scope == StartupIds.User ? locations.UserFolder : locations.MachineFolder, target.Name);

    // Only Direct entries get here, and those are this user's: the user's Run key and folder.
    private bool IsRegistered(StartupTarget target)
    {
        if (target.Source == StartupSource.StartupFolder)
        {
            return File.Exists(FolderFile(target));
        }
        using var run = locations.User.OpenSubKey(WindowsStartupReader.RunKey);
        return run?.GetValue(target.Name) is string;
    }
}
