using System.Globalization;
using Avalonia.Threading;
using MemoryMap.Core;

namespace MemoryMap.App;

public sealed record LegendItem(MemoryGroup Group, string Label, string Size, string Share);

/// <summary>What the tooltip says about the node under the pointer.</summary>
public sealed record HoverTip(string Title, string Text);

/// <summary>A breadcrumb level above the node shown, which zooms back out to it.</summary>
public sealed record Crumb(string Key, string Name);

/// <summary>A context-menu command; disabled when <see cref="Run"/> is null. Run it through
/// <see cref="MainViewModel.RunAsync"/>, which reports a failure instead of throwing.</summary>
/// <param name="StartsGroup">Drawn with a separator above it.</param>
public sealed record MenuEntry(string Header, Func<Task>? Run, bool StartsGroup = false);

/// <summary>
/// The window's state. Takes breakdowns from <see cref="LiveMemory"/> (on a pool thread) and
/// applies them on the UI thread; a failed sample shows in <see cref="Status"/> while the last
/// good map stays on screen. Owns where the map is zoomed (<see cref="Navigation"/>) and runs
/// process actions off the UI thread, ending a process only after <see cref="Confirm"/> agrees.
/// Every sample also goes into the <see cref="History"/> and the low-memory alert, whichever tab is shown.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    public const int MapView = 0;
    public const int TableView = 1;
    public const int HistoryView = 2;
    public const int SettingsView = 3;

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
    private IReadOnlyList<Crumb> _ancestors = [];
    private string? _notice;
    private readonly LowMemoryAlert _alert = new();
    private bool _isLowMemory;
    private string _alertMessage = "";
    private string _trayTip = "Memory Map";
    private int _usedPercent;
    private bool _isOnScreen = true;

    public MainViewModel(LiveMemory? live = null, IProcessActions? actions = null, SettingsViewModel? settings = null)
    {
        _live = live;
        _actions = actions;
        Settings = settings ?? new SettingsViewModel();
        _notice = Settings.Problem;
        ApplySettings(Settings.Current);
        Settings.Changed += ApplySettings;
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

    public SettingsViewModel Settings { get; }

    public HistoryViewModel History { get; } = new();

    /// <summary>True from the sample that crossed the alert line until memory recovers (D011).</summary>
    public bool IsLowMemory { get => _isLowMemory; private set => Set(ref _isLowMemory, value); }

    /// <summary>What the low-memory alert says; set before <see cref="IsLowMemory"/> turns true.</summary>
    public string AlertMessage { get => _alertMessage; private set => Set(ref _alertMessage, value); }

    public string TrayTip { get => _trayTip; private set => Set(ref _trayTip, value); }

    /// <summary>False while the window is hidden in the tray or minimised: nothing is drawn, so
    /// nothing is built to be drawn, and sampling slows to the hidden refresh (D016). The window sets it.</summary>
    public bool IsOnScreen
    {
        get => _isOnScreen;
        set
        {
            if (!Set(ref _isOnScreen, value))
            {
                return;
            }
            UpdateInterval();
            UpdateChart();
        }
    }

    /// <summary>Share of RAM in use, 0-100, for the tray icon.</summary>
    public int UsedPercent { get => _usedPercent; private set => Set(ref _usedPercent, value); }

    public MemoryBreakdown? Breakdown { get => _breakdown; private set => Set(ref _breakdown, value); }

    /// <summary>The node the map shows: the root, or the group, app or "Other" zoomed into.</summary>
    public MapNode? ViewRoot { get => _viewRoot; private set => Set(ref _viewRoot, value); }

    /// <summary>The breadcrumb levels above <see cref="ViewRoot"/>, outermost first.</summary>
    public IReadOnlyList<Crumb> Ancestors { get => _ancestors; private set => SetList(ref _ancestors, value); }

    public bool CanGoBack => Ancestors.Count > 0;

    /// <summary>The outcome of the last process action, until dismissed; null when there is none.</summary>
    public string? Notice { get => _notice; private set => Set(ref _notice, value); }

    /// <summary>Asks the user before a process is ended; the window supplies it. Without it nothing is ended.</summary>
    public Func<ConfirmRequest, Task<bool>>? Confirm { get; set; }

    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    public string Status { get => _status; private set => Set(ref _status, value); }

    /// <summary>True while the map shows an older sample because the latest one failed.</summary>
    public bool IsStale { get => _isStale; private set => Set(ref _isStale, value); }

    public IReadOnlyList<LegendItem> Legend { get => _legend; private set => SetList(ref _legend, value); }

    /// <summary>The group legend explains the map and table; the chart has its own.</summary>
    public bool ShowsGroupLegend => ViewIndex is MapView or TableView;

    /// <summary>The table view's rows; built only while that view is shown.</summary>
    public IReadOnlyList<TableRow> Rows { get => _rows; private set => SetList(ref _rows, value); }

    /// <summary>Null when nothing is under the pointer.</summary>
    public HoverTip? Hover { get => _hover; private set => Set(ref _hover, value); }

    /// <summary>The selected tab: <see cref="MapView"/>, <see cref="TableView"/>, <see cref="HistoryView"/> or <see cref="SettingsView"/>.</summary>
    public int ViewIndex
    {
        get => _viewIndex;
        set
        {
            Set(ref _viewIndex, value);
            Raise(nameof(ShowsGroupLegend));
            UpdateRows();
            UpdateChart();
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
        Status = $"Updated {Format.Clock(breakdown.Snapshot.Taken)}"
            + (breakdown.Skew > 0 ? $"  ·  sampling skew {Format.Bytes(breakdown.Skew)}" : "");
        IsStale = false;
        UpdateView();
        Legend = [.. breakdown.Groups.Select(g => new LegendItem(g.Group, g.Name, Format.Bytes(g.Bytes), Format.Percent(g.Bytes, breakdown.Total)))];
        UpdateRows();
        History.History.Add(breakdown);
        UpdateChart();
        TrayTip = LowMemoryAlert.TrayTip(sys);
        UsedPercent = (int)Math.Round(sys.UsedPercent);
        UpdateAlert();
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
        if (NodeCommands.Hint(node, _actions is not null) is { } hint)
        {
            lines = [.. lines, hint];
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
    public IReadOnlyList<MenuEntry> MenuFor(MapNode node) =>
        [.. NodeCommands.For(node, CanGoBack, _actions is not null).Select(option => new MenuEntry(option.Header,
            option.Enabled ? Command(option, node) : null, option.StartsGroup))];

    /// <summary>Runs a menu command. A failure becomes the <see cref="Notice"/>: a menu click has
    /// no caller to catch it, so an exception here would otherwise end the app.</summary>
    public async Task RunAsync(MenuEntry entry)
    {
        if (entry.Run is not { } run)
        {
            return;
        }
        try
        {
            await run();
        }
        catch (Exception e)
        {
            Notice = $"{entry.Header.TrimEnd('…')} failed: {e.Message}";
        }
    }

    /// <summary>Ends <paramref name="process"/>, once the user confirms.</summary>
    public async Task EndAsync(ProcessSample process)
    {
        if (_actions is not { } actions || Confirm is not { } confirm || !await confirm(ConfirmRequest.ToEnd(process)))
        {
            return;
        }
        var result = await Task.Run(() => actions.End(process));
        Notice = result.Message;
        if (result.Outcome is ActionOutcome.Done or ActionOutcome.AlreadyExited && _live is { } live)
        {
            try
            {
                // Show the freed memory now rather than at the next tick.
                await Task.Run(live.RefreshNow);
            }
            catch (ObjectDisposedException)
            {
                // The app is closing; there is nothing left to refresh.
            }
        }
    }

    /// <summary>Opens Explorer at the node's executable; says why when it cannot. Every process
    /// of an app shares the file, so one that has exited since the sample hands over to the next.</summary>
    public async Task ShowInExplorerAsync(MapNode node)
    {
        if (_actions is not { } actions)
        {
            return;
        }
        ActionResult? result = null;
        foreach (var process in ProcessTarget.ToLocate(node))
        {
            result = await Task.Run(() => actions.ShowInExplorer(process));
            if (result.Outcome != ActionOutcome.AlreadyExited)
            {
                break;
            }
        }
        if (result is { Succeeded: false })
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
        var couldGoBack = CanGoBack;
        Ancestors = [.. path.SkipLast(1).Select(n => new Crumb(n.Key, n.Name))];
        if (couldGoBack != CanGoBack)
        {
            Raise(nameof(CanGoBack));
        }
    }

    private Func<Task> Command(CommandOption option, MapNode node) => option.Command switch
    {
        NodeCommand.ZoomIn => () => Task.FromResult(ZoomInto(node)),
        NodeCommand.Back => () => Task.FromResult(Back()),
        NodeCommand.End => () => EndAsync(option.Target!),
        NodeCommand.Locate => () => ShowInExplorerAsync(node),
        _ => throw new ArgumentOutOfRangeException(nameof(option), option.Command, "no handler for this command"),
    };

    private void ApplySettings(AppSettings settings)
    {
        UpdateInterval();
        History.Window = TimeSpan.FromMinutes(settings.HistoryMinutes);
        UpdateChart();
        UpdateAlert();
    }

    private void UpdateInterval()
    {
        var interval = TimeSpan.FromSeconds(Settings.Current.IntervalSeconds(IsOnScreen));
        if (_live is not { } live || live.Interval == interval)
        {
            return;
        }
        try
        {
            var sooner = interval < live.Interval;
            live.Interval = interval;
            if (sooner)
            {
                // Coming back on screen: show the present now rather than up to a hidden refresh later.
                _ = Task.Run(() =>
                {
                    try
                    {
                        live.RefreshNow();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Closed meanwhile.
                    }
                });
            }
        }
        catch (ObjectDisposedException)
        {
            // The app is closing.
        }
    }

    private void UpdateAlert()
    {
        if (Breakdown is not { } breakdown)
        {
            return;
        }
        var settings = Settings.Current;
        switch (_alert.Update(breakdown.Snapshot.System, settings.AlertBelowPercent, settings.AlertsEnabled))
        {
            case AlertChange.Raised:
                AlertMessage = LowMemoryAlert.Message(breakdown, settings.AlertBelowPercent);
                IsLowMemory = true;
                break;
            case AlertChange.Cleared:
                IsLowMemory = false;
                break;
        }
    }

    // The chart is rebuilt only while it is on screen; the history is recorded regardless.
    private void UpdateChart()
    {
        if (ViewIndex == HistoryView && IsOnScreen)
        {
            History.Rebuild();
        }
        else
        {
            History.Release();
        }
    }

    private void UpdateRows() =>
        Rows = ViewIndex == TableView && Breakdown is not null ? Describe.Table(Breakdown) : [];
}
