using System.Runtime.Versioning;
using System.Security;
using MemoryMap.Core;
using Microsoft.Win32;

namespace MemoryMap.Windows.Startup;

/// <summary>What a flag write came to; the values are the elevated helper's exit codes.</summary>
internal enum FlagWrite
{
    Switched = 0,
    Failed = 1,
    Refused = 2,
    Gone = 3,
    NotKept = 4,
}

/// <summary>
/// Writes a registration's StartupApproved flag and reads it back (D021). Used in this process
/// for this user's entries, and by the elevated helper for machine-wide ones (D024).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class StartupFlags
{
    public static (FlagWrite Outcome, string? Error) Write(StartupLocations locations, StartupTarget target, bool enabled)
    {
        try
        {
            // A flag for something no longer registered would only be clutter; say so instead.
            if (!IsRegistered(locations, target))
            {
                return (FlagWrite.Gone, null);
            }
            using var approval = target.Root(locations).CreateSubKey(target.ApprovalPath, writable: true);
            approval.SetValue(target.Name, StartupApproval.Flag(enabled, DateTimeOffset.Now), RegistryValueKind.Binary);
            return (StartupApproval.IsEnabled(approval.GetValue(target.Name)) == enabled ? FlagWrite.Switched : FlagWrite.NotKept, null);
        }
        // ArgumentException: a name the registry refuses.
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            return (FlagWrite.Failed, e.Message);
        }
    }

    /// <summary>Whether the Run value or the folder file is still there.</summary>
    private static bool IsRegistered(StartupLocations locations, StartupTarget target)
    {
        if (target.Source == StartupSource.StartupFolder)
        {
            return File.Exists(target.FolderFile(locations));
        }
        using var run = target.Root(locations).OpenSubKey(target.RunKey);
        return run?.GetValue(target.Name) is string;
    }
}
