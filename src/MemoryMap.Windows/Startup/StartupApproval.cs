namespace MemoryMap.Windows.Startup;

/// <summary>
/// The on/off flag Task Manager and Settings keep for each Run value and Startup-folder file,
/// under Explorer\StartupApproved. It is undocumented; the rule here was checked against this
/// PC's Settings page (D018).
/// </summary>
internal static class StartupApproval
{
    // Each under the root (user or machine) the entry itself is registered in.
    public const string Run = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public const string Run32 = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    public const string Folder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    /// <summary>
    /// Enabled unless the flag's first byte is odd. No flag means enabled: Windows only writes
    /// one once the entry has been switched. Seen on this PC: 02 on, 03 and 07 off (07 is
    /// Windows Security's icon, shown Off in Settings).
    /// </summary>
    public static bool IsEnabled(object? flag) => flag is not byte[] { Length: > 0 } bytes || (bytes[0] & 1) == 0;
}
