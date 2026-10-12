using System.Runtime.Versioning;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// The one-shot elevated helper: <c>MemoryMap.exe --startup-helper &lt;id&gt; on|off</c>, started by
/// <see cref="ShellStartupLauncher.RunElevated"/> for one machine-wide change. It writes that
/// one flag, exits with a <see cref="FlagWrite"/> code, and shows no window. See D024.
/// </summary>
[SupportedOSPlatform("windows")]
public static class StartupHelper
{
    public const string Verb = "--startup-helper";

    /// <summary>The exit code when <paramref name="args"/> ask for the helper; null for a normal start.</summary>
    public static int? Run(string[] args) =>
        args is [Verb, ..] ? Execute(args, StartupLocations.ForCurrentUser()) : null;

    internal static int Execute(string[] args, StartupLocations locations)
    {
        if (args is not [Verb, var id, "on" or "off"])
        {
            return (int)FlagWrite.Refused;
        }
        StartupTarget target;
        try
        {
            target = StartupIds.Parse(id);
        }
        catch (ArgumentException)
        {
            return (int)FlagWrite.Refused;
        }
        // Run as an administrator, HKCU and the Startup folder are the administrator's, not the
        // user's who asked; this user's entries never need the helper, so it refuses them.
        if (target.IsUser)
        {
            return (int)FlagWrite.Refused;
        }
        return (int)StartupFlags.Write(locations, target, args[2] == "on").Outcome;
    }
}
