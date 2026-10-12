// memmap-dump: prints one memory breakdown as text or JSON. Used to cross-check the
// sampler against Windows' own figures and as a scriptable view of the same data the app shows.
using System.Text.Json;
using MemoryMap.Core;
using MemoryMap.Windows;
using MemoryMap.Windows.Startup;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("memmap-dump runs on Windows only.");
    return 2;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;
var json = args.Contains("--json");
var top = 10;
var topIndex = Array.IndexOf(args, "--top");
if (topIndex >= 0 && (topIndex + 1 >= args.Length || !int.TryParse(args[topIndex + 1], out top)))
{
    Console.Error.WriteLine("Usage: memmap-dump [--json] [--top N] [--bench | --startup | --startup-set <id> on|off]");
    return 2;
}

using var sampler = new WindowsMemorySampler();

if (args.Contains("--bench"))
{
    // Cost of one refresh (sample + breakdown), for the 1 s refresh budget.
    Breakdown.Build(sampler.Sample());
    const int runs = 50;
    var cpuBefore = Environment.CpuUsage.TotalTime;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    for (var i = 0; i < runs; i++)
    {
        Breakdown.Build(sampler.Sample());
    }
    watch.Stop();
    var cpu = Environment.CpuUsage.TotalTime - cpuBefore;
    Console.WriteLine($"{runs} refreshes: {watch.Elapsed.TotalMilliseconds / runs:N2} ms wall, {cpu.TotalMilliseconds / runs:N2} ms CPU per refresh");
    return 0;
}

var setIndex = Array.IndexOf(args, "--startup-set");
if (setIndex >= 0)
{
    // Switches one entry by id, through the app's own code; scripts/startup-roundtrip.ps1 uses it
    // on throwaway entries to check that Windows Settings agrees with the change.
    if (setIndex + 2 >= args.Length || args[setIndex + 2] is not ("on" or "off"))
    {
        Console.Error.WriteLine("Usage: memmap-dump --startup-set <id> on|off");
        return 2;
    }
    var entry = new WindowsStartupReader().Read().Entries.FirstOrDefault(e => e.Id == args[setIndex + 1]);
    if (entry is null)
    {
        Console.Error.WriteLine($"No startup entry has the id {args[setIndex + 1]}.");
        return 1;
    }
    var result = new WindowsStartupActions().SetEnabled(entry, args[setIndex + 2] == "on");
    Console.WriteLine(result.Message);
    return result.Succeeded ? 0 : 1;
}

if (args.Contains("--startup"))
{
    // Every program that starts at sign-in, with what it costs now; scripts/startup-crosscheck.ps1
    // compares this against Windows Settings' Startup apps page.
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var list = new WindowsStartupReader().Read();
    var readMs = watch.Elapsed.TotalMilliseconds;
    var processes = sampler.Sample().Processes;
    watch.Restart();
    var paths = new WindowsProcessPaths();
    var usage = StartupMatching.Match(list.Entries, processes, paths);
    var matchMs = watch.Elapsed.TotalMilliseconds;
    // The app matches every sample while its Startup tab is shown, with the paths already known.
    watch.Restart();
    StartupMatching.Match(list.Entries, sampler.Sample().Processes, paths);
    var warmMs = watch.Elapsed.TotalMilliseconds;
    if (json)
    {
        var output = list.Entries.Select(e => new
        {
            e.Id,
            e.Name,
            e.Publisher,
            e.Command,
            e.Location,
            source = e.Source.ToString(),
            scope = e.Scope.ToString(),
            e.Enabled,
            @switch = e.Switch.ToString(),
            e.ExecutablePath,
            e.ProgramFolder,
            e.Problem,
            e.CountsProcesses,
            running = usage[e.Id].Processes.Count,
            privateWorkingSet = usage[e.Id].PrivateWorkingSet,
        });
        Console.WriteLine(JsonSerializer.Serialize(new { entries = output, list.Unreadable }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    foreach (var e in list.Entries)
    {
        var use = usage[e.Id];
        Console.WriteLine($"{(e.Enabled ? "on " : "off")} {e.Name,-40} {e.Switch,-10} {StartupText.Status(e, use),-24} {StartupText.Memory(e, use),9}  {e.Location}");
        if (e.Problem is not null)
        {
            Console.WriteLine($"    ! {e.Problem}");
        }
    }
    foreach (var source in list.Unreadable)
    {
        Console.WriteLine($"Could not read {source}");
    }
    Console.WriteLine($"{list.Entries.Count} entries; read {readMs:N0} ms, matched to processes {matchMs:N0} ms (next sample {warmMs:N1} ms)");
    return 0;
}

var breakdown = Breakdown.Build(sampler.Sample());
var sys = breakdown.Snapshot.System;

if (json)
{
    var output = new
    {
        taken = breakdown.Snapshot.Taken,
        system = sys,
        used = sys.Used,
        skew = breakdown.Skew,
        groups = breakdown.Groups.Select(g => new
        {
            group = g.Group.ToString(),
            label = g.Name,
            g.Bytes,
            items = g.Children.Select(i => new { i.Key, i.Name, i.Bytes, processes = i.ProcessCount }),
        }),
    };
    Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

Console.WriteLine($"Snapshot {breakdown.Snapshot.Taken:yyyy-MM-dd HH:mm:ss}, uptime {sys.Uptime.TotalDays:N1} days");
Console.WriteLine($"Total {Format.Bytes(sys.TotalPhysical, true)}  In use {Format.Bytes(sys.Used, true)} ({Format.Percent(sys.Used, sys.TotalPhysical)})  Available {Format.Bytes(sys.Available, true)}");
Console.WriteLine($"Commit {Format.Bytes(sys.CommitTotal, true)} of {Format.Bytes(sys.CommitLimit, true)}  Paged pool total {Format.Bytes(sys.PagedPoolTotal)}");
Console.WriteLine($"Processes sampled: {breakdown.Snapshot.Processes.Count}");
if (breakdown.Skew > 0)
{
    Console.WriteLine($"Sampling skew: processes and kernel exceed in-use memory by {Format.Bytes(breakdown.Skew)}");
}
foreach (var group in breakdown.Groups)
{
    Console.WriteLine();
    Console.WriteLine($"{group.Name,-22} {Format.Bytes(group.Bytes),10} {Format.Percent(group.Bytes, breakdown.Total),7}");
    foreach (var item in group.Children.Take(top))
    {
        var count = item.ProcessCount > 0 ? $"x{item.ProcessCount}" : "";
        Console.WriteLine($"  {item.Name,-30} {Format.Bytes(item.Bytes),10} {count,6}");
    }
    if (group.Children.Count > top)
    {
        Console.WriteLine($"  … {group.Children.Count - top} more");
    }
}
return 0;
