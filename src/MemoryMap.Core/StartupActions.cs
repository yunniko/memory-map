namespace MemoryMap.Core;

/// <summary>
/// Things the user can do to a startup entry. Implementations only ever change the entry's
/// on/off flag, the same flag Windows' own switches change: nothing is deleted, so every change
/// can be switched back (G-002 criterion 3).
/// </summary>
public interface IStartupActions
{
    /// <summary>Switches the entry on or off and reads the flag back to confirm it held.</summary>
    ActionResult SetEnabled(StartupEntry entry, bool enabled);

    /// <summary>Opens the file manager at the entry's program file (or its Startup-folder file).</summary>
    ActionResult ShowInExplorer(StartupEntry entry);
}

/// <summary>The sentences startup actions report, worded the same by every implementation.</summary>
public static class StartupResult
{
    public static ActionResult Switched(StartupEntry e, bool enabled) => new(ActionOutcome.Done,
        enabled ? $"{e.Name} will start at sign-in." : $"{e.Name} will no longer start at sign-in. Switch it back on at any time.");

    public static ActionResult Gone(StartupEntry e) => new(ActionOutcome.AlreadyExited,
        $"{e.Name} is no longer registered to start at sign-in; the list has been refreshed.");

    /// <summary>The administrator prompt for a machine-wide entry was answered No.</summary>
    public static ActionResult Declined(StartupEntry e) => new(ActionOutcome.Refused,
        $"{e.Name} was not switched: the administrator prompt was declined, so nothing changed.");

    /// <summary>A Store app's switch opened Windows Settings, where the change is made.</summary>
    public static ActionResult InSettings(StartupEntry e) => new(ActionOutcome.Refused,
        $"{e.Name} is a Store app: switch it in the Windows Settings page that just opened (Apps > Startup).");

    public static ActionResult Failed(StartupEntry e, string verb, string reason) =>
        new(ActionOutcome.Failed, $"Could not {verb} {e.Name}: {reason}");
}

/// <summary>How an entry's state reads in the startup list and in memmap-dump, worded once.</summary>
public static class StartupText
{
    public const string NotCounted = "Not counted";

    /// <summary>"Running, 2 processes", "Not running", or <see cref="NotCounted"/> for a host program (D020).</summary>
    public static string Status(StartupEntry entry, StartupUsage usage) =>
        !entry.CountsProcesses ? NotCounted
        : !usage.Running ? "Not running"
        : usage.Processes.Count == 1 ? "Running"
        : $"Running, {usage.Processes.Count} processes";

    /// <summary>The memory it holds now, or empty when nothing of it runs or it is not counted.</summary>
    public static string Memory(StartupEntry entry, StartupUsage usage) =>
        entry.CountsProcesses && usage.Running ? Format.Bytes(usage.PrivateWorkingSet) : "";

    /// <summary>What flipping the switch will do, for its tooltip.</summary>
    public static string SwitchHint(StartupEntry entry) => entry.Switch switch
    {
        StartupSwitch.Direct => entry.Enabled ? "Starts when you sign in; switch off to stop that" : "Switched off; switch on to start it at sign-in",
        StartupSwitch.NeedsAdmin => "It starts for every user of this PC; switching it asks for administrator rights for that one change",
        _ => "A Store app: the switch opens Windows Settings > Apps > Startup, where it is changed",
    };

    /// <summary>Explains <see cref="NotCounted"/>.</summary>
    public const string NotCountedHint =
        "It starts a Windows program that also runs other things (cmd, rundll32, a script host), so its processes cannot be told apart";
}
