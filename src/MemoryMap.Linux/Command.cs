using System.ComponentModel;
using System.Diagnostics;

namespace MemoryMap.Linux;

/// <summary>What a command printed and how it ended; one that ran out of time has exit code -1.</summary>
internal sealed record CommandResult(int ExitCode, string Output, string Error, bool TimedOut = false);

/// <summary>Runs a desktop or system tool and collects its output, with a time limit.</summary>
internal static class Command
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>The result; null if <paramref name="program"/> is not installed. One that does
    /// not finish in time is killed.</summary>
    public static CommandResult? Run(string program, params string[] args)
    {
        var info = new ProcessStartInfo(program, args)
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
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
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
                return new CommandResult(-1, "", $"{program} did not answer within {Timeout.TotalSeconds:0} s.", TimedOut: true);
            }
            return new CommandResult(process.ExitCode, output.Result, error.Result);
        }
        catch (Win32Exception)
        {
            return null; // not installed
        }
    }
}
