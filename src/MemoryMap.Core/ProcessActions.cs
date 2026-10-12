namespace MemoryMap.Core;

public enum ActionOutcome
{
    Done,

    /// <summary>The process is gone, or its PID now belongs to a different process.</summary>
    AlreadyExited,

    /// <summary>The OS refused: another user's process, or an elevated or protected one.</summary>
    AccessDenied,

    /// <summary>Memory Map will not do it (a critical system process, or itself).</summary>
    Refused,

    Failed,
}

/// <summary>
/// What happened, in a sentence fit to show the user as is. The sentences are built here, so
/// every <see cref="IProcessActions"/> implementation words its outcomes the same way.
/// </summary>
public sealed record ActionResult(ActionOutcome Outcome, string Message)
{
    public bool Succeeded => Outcome == ActionOutcome.Done;

    public static ActionResult Ended(ProcessSample p) => new(ActionOutcome.Done, $"Ended {p.Label}.");

    public static ActionResult Shown(string path) => new(ActionOutcome.Done, $"Showed {path}.");

    public static ActionResult Exited(ProcessSample p) => new(ActionOutcome.AlreadyExited, $"{p.Label} has already exited.");

    public static ActionResult Denied(ProcessSample p, string verb) => new(ActionOutcome.AccessDenied,
        $"Windows denied access to {verb} {p.Label}. It runs as another user, elevated, or protected; "
        + "Memory Map would need to run as administrator, and protected processes refuse even then.");

    /// <summary>Linux: the process belongs to another account, and the app never asks for root.</summary>
    public static ActionResult NotYours(ProcessSample p, string verb) => new(ActionOutcome.AccessDenied,
        $"{p.Label} runs as another user. Memory Map can {verb} only your own processes; it does not ask for root.");

    public static ActionResult Critical(ProcessSample p) => new(ActionOutcome.Refused,
        $"{p.Label} is a critical system process: ending it would crash the system, so Memory Map will not.");

    public static ActionResult Self() => new(ActionOutcome.Refused, "Memory Map does not end itself; close its window instead.");

    public static ActionResult Failed(ProcessSample p, string verb, string reason) =>
        new(ActionOutcome.Failed, $"Could not {verb} {p.Label}: {reason}");
}

/// <summary>
/// Things the user can do to a process seen on the map. Every call checks that the PID
/// still belongs to the process that was sampled (same start time) before touching it.
/// </summary>
public interface IProcessActions
{
    ActionResult End(ProcessSample process);

    /// <summary>Opens the desktop's file manager at the process's executable.</summary>
    ActionResult ShowFileLocation(ProcessSample process);
}

/// <summary>Which process an action on a map node would touch.</summary>
public static class ProcessTarget
{
    /// <summary>
    /// The one process that ending this node would end: a process, or an app running exactly
    /// one. Null otherwise, so one click never ends a whole group of processes.
    /// </summary>
    public static ProcessSample? ToEnd(MapNode node) =>
        IsProcessNode(node) && node.ProcessCount == 1 ? node.Processes().First() : null;

    /// <summary>
    /// Processes whose executable is this node's, largest first: every process of an app shares
    /// it, so if one has exited the next will do. Empty for nodes that are not one executable.
    /// </summary>
    public static IEnumerable<ProcessSample> ToLocate(MapNode node) => IsProcessNode(node) ? node.Processes() : [];

    /// <summary>A single process, or all processes of one executable.</summary>
    public static bool IsProcessNode(MapNode node) => node.Kind is NodeKind.Process or NodeKind.App;
}

/// <summary>A question put to the user before an action that cannot be undone.</summary>
public sealed record ConfirmRequest(string Question, string Detail, string ConfirmLabel)
{
    public static ConfirmRequest ToEnd(ProcessSample p) => new(
        $"End {p.Label}?", $"{p.Name} closes at once, without saving: anything unsaved in it is lost.", "End process");
}

public enum NodeCommand
{
    ZoomIn,
    Back,
    End,
    Locate,
}

/// <summary>One context-menu command for a node; <paramref name="Target"/> is the process End acts on.</summary>
/// <param name="StartsGroup">Drawn with a separator above it.</param>
public sealed record CommandOption(NodeCommand Command, string Header, bool Enabled, bool StartsGroup = false, ProcessSample? Target = null);

/// <summary>
/// What can be done with a node, for the context menu and the hover hint alike, so the hint
/// never promises a command the menu does not have.
/// </summary>
public static class NodeCommands
{
    public static IReadOnlyList<CommandOption> For(MapNode node, bool canGoBack, bool processActions)
    {
        var options = new List<CommandOption>();
        if (Navigation.CanZoomInto(node))
        {
            options.Add(new(NodeCommand.ZoomIn, "Zoom in", true));
        }
        if (canGoBack)
        {
            options.Add(new(NodeCommand.Back, "Back", true));
        }
        if (processActions && ProcessTarget.IsProcessNode(node))
        {
            var group = options.Count > 0;
            options.Add(ProcessTarget.ToEnd(node) is { } p
                ? new(NodeCommand.End, $"End {p.Label}…", true, group, p)
                : new(NodeCommand.End, $"End process: zoom in to pick one of its {node.ProcessCount}", false, group));
            options.Add(new(NodeCommand.Locate, "Open file location", true));
        }
        return options;
    }

    /// <summary>The tooltip's last line: how to reach the node's commands; null when it has none.</summary>
    public static string? Hint(MapNode node, bool processActions) =>
        (Navigation.CanZoomInto(node), processActions && ProcessTarget.IsProcessNode(node)) switch
        {
            (true, true) => "Click to zoom in · right-click for actions",
            (true, false) => "Click to zoom in",
            (false, true) => "Right-click for actions",
            _ => null,
        };
}
