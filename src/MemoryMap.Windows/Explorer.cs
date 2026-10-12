using System.Diagnostics;

namespace MemoryMap.Windows;

/// <summary>Hands a file or page to the Windows shell: "Open file location" (map and startup list) and Settings pages.</summary>
internal static class Explorer
{
    /// <summary>Opens File Explorer with <paramref name="path"/> selected; null when it started, else why not.</summary>
    // Explorer's own syntax; the path is quoted because it may contain commas or spaces.
    public static string? Select(string path) =>
        Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false }, "Explorer did not start");

    /// <summary>Opens a URI such as a Settings page with its registered handler; null when it opened, else why not.</summary>
    public static string? Open(string uri) =>
        Start(new ProcessStartInfo(uri) { UseShellExecute = true }, "Windows Settings did not open");

    private static string? Start(ProcessStartInfo info, string failure)
    {
        try
        {
            using var _ = Process.Start(info);
            return null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return $"{failure} ({e.Message}).";
        }
    }
}
