using System.Diagnostics;

namespace MemoryMap.Windows;

/// <summary>Opens File Explorer with a file selected; shared by the map's and the startup list's "Open file location".</summary>
internal static class Explorer
{
    /// <summary>Null when Explorer started; otherwise why it did not, as a sentence.</summary>
    public static string? Select(string path)
    {
        try
        {
            // Explorer's own syntax; the path is quoted because it may contain commas or spaces.
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = false });
            return null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return $"Explorer did not start ({e.Message}).";
        }
    }
}
