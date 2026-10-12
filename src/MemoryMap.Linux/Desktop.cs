using System.ComponentModel;
using System.Diagnostics;

namespace MemoryMap.Linux;

/// <summary>
/// The desktop session, reached through its standard tools: <c>dbus-send</c> for the session bus
/// and <c>xdg-open</c>. Both ship with every desktop, and the package depends on them; without
/// them the calls report failure rather than throw.
/// </summary>
public static class Desktop
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether the desktop shows tray icons now: a StatusNotifierWatcher (KDE, GNOME with the
    /// AppIndicator extension, Xfce, Cinnamon) with a host registered to draw them.
    /// </summary>
    public static bool HasTrayHost() =>
        Bus("--dest=org.kde.StatusNotifierWatcher", "/StatusNotifierWatcher", "org.freedesktop.DBus.Properties.Get",
            "string:org.kde.StatusNotifierWatcher", "string:IsStatusNotifierHostRegistered")
        is { } reply && reply.Contains("boolean true", StringComparison.Ordinal);

    /// <summary>
    /// Opens the file manager at <paramref name="path"/>, selected where the file manager offers
    /// FileManager1 (Nautilus, Dolphin, Nemo, Caja, Thunar); else opens its folder with xdg-open.
    /// Returns why it failed, or null.
    /// </summary>
    public static string? ShowFile(string path)
    {
        // dbus-send splits array items at commas, so a comma is escaped like the rest of the URI.
        var uri = new Uri(path).AbsoluteUri.Replace(",", "%2C", StringComparison.Ordinal);
        if (Bus("--dest=org.freedesktop.FileManager1", "/org/freedesktop/FileManager1",
            "org.freedesktop.FileManager1.ShowItems", $"array:string:{uri}", "string:") is not null)
        {
            return null;
        }
        // xdg-open may stay until the program it opened exits: still running at the timeout is success.
        var info = new ProcessStartInfo("xdg-open") { ArgumentList = { Path.GetDirectoryName(path) ?? path }, UseShellExecute = false };
        try
        {
            using var process = Process.Start(info);
            return process is null || !process.WaitForExit(Timeout) || process.ExitCode == 0
                ? null
                : $"xdg-open could not open the folder (exit code {process.ExitCode}).";
        }
        catch (Win32Exception)
        {
            return "no file manager could be opened: xdg-open is not installed.";
        }
    }

    /// <summary>A method call on the session bus: the reply, or null if it failed or no one answered.</summary>
    private static string? Bus(params string[] call)
    {
        // stderr is captured only to keep "no such service" out of the app's own output.
        var info = new ProcessStartInfo("dbus-send", ["--session", "--print-reply", .. call])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }
            var reply = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // it exited after all, between the wait and the kill
                }
                return null;
            }
            return process.ExitCode == 0 ? reply.Result : null;
        }
        catch (Win32Exception)
        {
            return null; // dbus-send is not installed
        }
    }
}
