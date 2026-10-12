namespace MemoryMap.Core;

public enum AlertChange
{
    None,
    Raised,
    Cleared,
}

/// <summary>
/// Decides when available memory is low enough to warn about. It warns once on the way down and
/// re-arms only after available memory climbs <see cref="RearmMargin"/> points above the
/// threshold, so memory hovering at the line does not warn every second (D011).
/// </summary>
public sealed class LowMemoryAlert
{
    /// <summary>Percentage points of RAM above the threshold that clear the alert.</summary>
    public const double RearmMargin = 2;

    public bool IsActive { get; private set; }

    public AlertChange Update(SystemMemory system, int thresholdPercent, bool enabled)
    {
        var available = system.AvailablePercent;
        var low = enabled && available < thresholdPercent;
        var recovered = !enabled || available >= thresholdPercent + RearmMargin;
        if (!IsActive && low)
        {
            IsActive = true;
            return AlertChange.Raised;
        }
        if (IsActive && recovered)
        {
            IsActive = false;
            return AlertChange.Cleared;
        }
        return AlertChange.None;
    }

    /// <summary>What the alert says: how low memory is, and the biggest app, the likeliest thing to close.</summary>
    public static string Message(MemoryBreakdown breakdown, int thresholdPercent)
    {
        var sys = breakdown.Snapshot.System;
        var text = $"Only {Format.Bytes(sys.Available, true)} of RAM is available ({Format.Percent(sys.Available, sys.TotalPhysical)}), "
            + $"below the {thresholdPercent}% alert.";
        var biggest = breakdown.Root.Apps().MaxBy(a => a.Bytes);
        return biggest is null ? text : $"{text} The biggest app is {biggest.Name} at {Format.Bytes(biggest.Bytes)}.";
    }

    /// <summary>The tray icon's tooltip: the headline figures in a line or two.</summary>
    public static string TrayTip(SystemMemory sys) =>
        $"Memory Map: {Format.Percent(sys.Used, sys.TotalPhysical)} in use"
        + $"{Environment.NewLine}{Format.Bytes(sys.Used, true)} of {Format.Bytes(sys.TotalPhysical, true)}, {Format.Bytes(sys.Available, true)} available";
}
