namespace MemoryMap.Core;

public enum ActionOutcome
{
    Done,

    /// <summary>The process is gone, or its PID now belongs to a different process.</summary>
    AlreadyExited,

    /// <summary>Windows refused: another user's or an elevated or protected process.</summary>
    AccessDenied,

    /// <summary>Memory Map will not do it (a critical system process, or itself).</summary>
    Refused,

    Failed,
}

/// <summary>What happened, in a sentence fit to show the user as is.</summary>
public sealed record ActionResult(ActionOutcome Outcome, string Message)
{
    public bool Succeeded => Outcome == ActionOutcome.Done;
}

/// <summary>
/// Things the user can do to a process seen on the map. Every call checks that the PID
/// still belongs to the process that was sampled (same start time) before touching it.
/// </summary>
public interface IProcessActions
{
    ActionResult End(ProcessSample process);

    ActionResult ShowInExplorer(ProcessSample process);
}

/// <summary>Which process an action on a map node would touch.</summary>
public static class ProcessTarget
{
    /// <summary>
    /// The one process that ending this node would end: a process, or an app running exactly
    /// one. Null otherwise, so one click never ends a whole group of processes.
    /// </summary>
    public static ProcessSample? ToEnd(MapNode node) =>
        node.Kind is NodeKind.Process or NodeKind.App && node.ProcessCount == 1 ? node.Processes().First() : null;

    /// <summary>A process whose executable is this node's: any process of an app shares it.</summary>
    public static ProcessSample? ToLocate(MapNode node) =>
        node.Kind is NodeKind.Process or NodeKind.App ? node.Processes().FirstOrDefault() : null;
}
