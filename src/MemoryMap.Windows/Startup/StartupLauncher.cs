using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace MemoryMap.Windows.Startup;

/// <summary>
/// What a startup switch hands to Windows itself: this program run again elevated (for a
/// machine-wide entry), and the Settings page (for a Store app). Tests replace it, so no
/// administrator prompt or Settings window appears during a test run.
/// </summary>
public interface IStartupLauncher
{
    /// <summary>
    /// Runs this program again as administrator with <paramref name="arguments"/> and waits for it.
    /// Its exit code, or null when the administrator prompt was declined.
    /// </summary>
    int? RunElevated(IReadOnlyList<string> arguments);

    /// <summary>Opens a page of Windows Settings; null when it opened, else why not.</summary>
    string? OpenSettings(string uri);
}

/// <summary>The real launcher: the Windows shell's "runas" verb and URI handler.</summary>
[SupportedOSPlatform("windows")]
public sealed class ShellStartupLauncher : IStartupLauncher
{
    // ERROR_CANCELLED: the user chose No on the administrator prompt.
    private const int Cancelled = 1223;

    public int? RunElevated(IReadOnlyList<string> arguments)
    {
        try
        {
            using var helper = Process.Start(Elevated(arguments))
                ?? throw new InvalidOperationException("Windows started no process.");
            helper.WaitForExit();
            return helper.ExitCode;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == Cancelled)
        {
            return null;
        }
    }

    public string? OpenSettings(string uri) => Explorer.Open(uri);

    // ArgumentList quotes each argument, so an id with spaces arrives whole.
    private static ProcessStartInfo Elevated(IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("This program's path is unknown."))
        {
            // The shell is what shows the administrator prompt; it also hides the helper's window.
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Verb = "runas",
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }
        return info;
    }
}
