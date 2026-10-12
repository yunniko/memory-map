using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using MemoryMap.Core;

namespace MemoryMap.App;

public sealed record LegendItem(MemoryGroup Group, string Label, string Size, string Share);

/// <summary>What the tooltip says about the node under the pointer.</summary>
public sealed record HoverTip(string Title, string Text);

/// <summary>One level of the breadcrumb; the last one is the node shown.</summary>
public sealed record Crumb(string Key, string Name, bool IsLast);

/// <summary>A context-menu command; disabled when <see cref="Run"/> is null.</summary>
/// <param name="StartsGroup">Drawn with a separator above it.</param>
public sealed record MenuEntry(string Header, Func<Task>? Run, bool StartsGroup = false);

/// <summary>
/// The window's state. Takes breakdowns from <see cref="LiveMemory"/> (on a pool thread) and
/// applies them on the UI thread; a failed sample shows in <see cref="Status"/> while the last
/// good map stays on screen. Owns where the map is zoomed (<see cref="Navigation"/>) and runs
/// process actions off the UI thread, ending a process only after <see cref="Confirm"/> agrees.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public const int MapView = 0;
    public const int TableView = 1;

    private MemoryBreakdown? _breakdown;
    private MemoryBreakdown? _pending;
    private string _summary = "Sampling…";
    private string _status = "";
    private bool _isStale;
    private IReadOnlyList<LegendItem> _legend = [];
    private IReadOnlyList<TableRow> _rows = [];
    private HoverTip? _hover;
    private int _viewIndex = MapView;
    private readonly LiveMemory? _live;
    private readonly IProcessActions? _actions;
    private readonly Navigation _navigation = new();
    private MapNode? _viewRoot;
    private IReadOnlyList<Crumb> _breadcrumb = [];
    private bool _canGoBack;
    private string? _notice;

    public MainViewModel(LiveMemory? live = null, IProcessActions? actions = null)
    {
        _live = live;
        _actions = actions;
        if (live is not null)
        {
            // Latest wins: if the UI thread falls behind, older breakdowns are dropped, not queued.
            live.Updated += b =>
            {
                if (Interlocked.Exchange(ref _pending, b) is null)
                {
                    Dispatcher.UIThread.Post(() => Apply(Interlocked.Exchange(ref _pending, null)!));
                }
            };
            live.Failed += e => Dispatcher.UIThread.Post(() => ShowFailure(e, DateTimeOffset.Now));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MemoryBreakdown? Breakdown { get => _breakdown; private set => Set(ref _breakdown, value); }

    /// <summary>The node the map shows: the root, or the group, app or "Other" zoomed into.</summary>
    public MapNode? ViewRoot { get => _viewRoot; private set => Set(ref _viewRoot, value); }

    public IReadOnlyList<Crumb> Breadcrumb { get => _breadcrumb; private set => SetList(ref _breadcrumb, value); }

    public bool CanGoBack { get => _canGoBack; private set => Set(ref _canGoBack, value); }

    /// <summary>The outcome of the last process action, until dismissed; null when there is none.</summary>
    public string? Notice { get => _notice; private set => Set(ref _notice, value); }

    /// <summary>Asks the user (title, message) before a process is ended; the window supplies it.
    /// Without it nothing is ended.</summary>
    public Func<string, string, Task<bool>>? Confirm { get; set; }

    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    public string Status { get => _status; private set => Set(ref _status, value); }

    /// <summary>True while the map shows an older sample because the latest one failed.</summary>
    public bool IsStale { get => _isStale; private set => Set(ref _isStale, value); }

    public IReadOnlyList<LegendItem> Legend { get => _legend; private set => SetList(ref _legend, value); }

    /// <summary>The table view's rows; built only while that view is shown.</summary>
    public IReadOnlyList<TableRow> Rows { get => _rows; private set => SetList(ref _rows, value); }

    /// <summary>Null when nothing is under the pointer.</summary>
    public HoverTip? Hover { get => _hover; private set => Set(ref _hover, value); }

    /// <summary>The selected tab: <see cref="MapView"/> or <see cref="TableView"/>.</summary>
    public int ViewIndex
    {
        get => _viewIndex;
        set
        {
            Set(ref _viewIndex, value);
            UpdateRows();
        }
    }

    public void Apply(MemoryBreakdown breakdown)
    {
        Breakdown = breakdown;
        var sys = breakdown.Snapshot.System;
        Summary = $"In use {Format.Bytes(sys.Used, true)} of {Format.Bytes(sys.TotalPhysical, true)} ({Format.Percent(sys.Used, sys.TotalPhysical)})"
            + $"  ·  Available {Format.Bytes(sys.Available, true)}"
            + $"  ·  Commit {Format.Bytes(sys.CommitTotal, true)} of {Format.Bytes(sys.CommitLimit, true)}"
            + $"  ·  {breakdown.Snapshot.Processes.Count} processes";
        Status = $"Updated {breakdown.Snapshot.Taken.ToString("T", CultureInfo.CurrentCulture)}"
            + (breakdown.Skew > 0 ? $"  ·  sampling skew {Format.Bytes(breakdown.Skew)}" : "");
        IsStale = false;
        UpdateView();
        Legend = [.. breakdown.Groups.Select(g => new LegendItem(g.Group, g.Name, Format.Bytes(g.Bytes), Format.Percent(g.Bytes, breakdown.Total)))];
        UpdateRows();
    }

    public void ShowFailure(Exception error, DateTimeOffset when)
    {
        IsStale = Breakdown is not null;
        Status = $"Sampling failed at {when.ToString("T", CultureInfo.CurrentCulture)}: {error.Message}"
            + (IsStale ? "  ·  showing the last good sample" : "");
    }

    public void SetHover(MapNode? node)
    {
        if (node is null || Breakdown is null)
        {
            Hover = null;
            return;
        }
        var (title, lines) = Describe.Tooltip(node, Breakdown.Total);
        var hints = new List<string>(2);
        if (Navigation.CanZoomInto(node))
        {
            hints.Add("Click to zoom in");
        }
        if (_actions is not null && ProcessTarget.ToLocate(node) is not null)
        {
            hints.Add(hints.Count == 0 ? "Right-click for actions" : "right-click for actions");
        }
        if (hints.Count > 0)
        {
            lines = [.. lines, string.Join(" · ", hints)];
        }
        Hover = new HoverTip(title, string.Join(Environment.NewLine, lines));
    }

    /// <summary>Shows <paramref name="node"/>'s children; false if it has none to show.</summary>
    public bool ZoomInto(MapNode node)
    {
        if (Breakdown is null || !_navigation.ZoomInto(Breakdown.Root, node))
        {
            return false;
        }
        UpdateView();
        return true;
    }

    /// <summary>Shows the breadcrumb level with <paramref name="key"/>.</summary>
    public void ZoomTo(string key)
    {
        _navigation.ZoomOutTo(key);
        UpdateView();
    }

    /// <summary>One level up; false when already at the top.</summary>
    public bool Back()
    {
        if (!_navigation.Back())
        {
            return false;
        }
        UpdateView();
        return true;
    }

    /// <summary>The context menu for a node: navigation, then the process actions that apply.</summary>
    public IReadOnlyList<MenuEntry> MenuFor(MapNode node)
    {
        var entries = new List<MenuEntry>();
        if (Navigation.CanZoomInto(node))
        {
            entries.Add(new("Zoom in", () => Task.FromResult(ZoomInto(node))));
        }
        if (CanGoBack)
        {
            entries.Add(new("Back", () => Task.FromResult(Back())));
        }
        if (_actions is not null && ProcessTarget.ToLocate(node) is not null)
        {
            entries.Add(ProcessTarget.ToEnd(node) is { } process
                ? new($"End {Label(process)}…", () => EndAsync(node), StartsGroup: entries.Count > 0)
                : new($"End process: zoom in to pick one of its {node.ProcessCount}", null, StartsGroup: entries.Count > 0));
            entries.Add(new("Open file location", () => ShowInExplorerAsync(node)));
        }
        return entries;
    }

    /// <summary>Ends the one process <paramref name="node"/> stands for, once the user confirms.</summary>
    public async Task EndAsync(MapNode node)
    {
        if (_actions is not { } actions || ProcessTarget.ToEnd(node) is not { } process || Confirm is not { } confirm)
        {
            return;
        }
        var agreed = await confirm($"End {Label(process)}?",
            $"{process.Name} closes at once, without saving: anything unsaved in it is lost.");
        if (!agreed)
        {
            return;
        }
        var result = await Task.Run(() => actions.End(process));
        Notice = result.Message;
        if (result.Outcome is ActionOutcome.Done or ActionOutcome.AlreadyExited && _live is { } live)
        {
            // Show the freed memory now rather than at the next tick.
            await Task.Run(live.RefreshNow);
        }
    }

    /// <summary>Opens Explorer at the node's executable; says why when it cannot.</summary>
    public async Task ShowInExplorerAsync(MapNode node)
    {
        if (_actions is not { } actions || ProcessTarget.ToLocate(node) is not { } process)
        {
            return;
        }
        var result = await Task.Run(() => actions.ShowInExplorer(process));
        if (!result.Succeeded)
        {
            Notice = result.Message;
        }
    }

    public void DismissNotice() => Notice = null;

    private void UpdateView()
    {
        if (Breakdown is null)
        {
            return;
        }
        var path = _navigation.Resolve(Breakdown.Root);
        ViewRoot = path[^1];
        Breadcrumb = [.. path.Select((n, i) => new Crumb(n.Key, n.Name, i == path.Count - 1))];
        CanGoBack = path.Count > 1;
    }

    private static string Label(ProcessSample process) => $"{process.Name} (PID {process.Pid})";

    private void UpdateRows() =>
        Rows = ViewIndex == TableView && Breakdown is not null ? Describe.Table(Breakdown) : [];

    // Rows are records: an unchanged list keeps the old instance, so the view does not rebuild its items.
    private void SetList<T>(ref IReadOnlyList<T> field, IReadOnlyList<T> value, [CallerMemberName] string? name = null)
    {
        if (!field.SequenceEqual(value))
        {
            Set(ref field, value, name);
        }
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
