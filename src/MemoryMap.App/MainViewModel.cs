using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using MemoryMap.Core;

namespace MemoryMap.App;

public sealed record LegendItem(MemoryGroup Group, string Label, string Size, string Share);

/// <summary>What the tooltip says about the node under the pointer.</summary>
public sealed record HoverTip(string Title, string Text);

/// <summary>
/// The window's state. Takes breakdowns from <see cref="LiveMemory"/> (on a pool thread) and
/// applies them on the UI thread; a failed sample shows in <see cref="Status"/> while the last
/// good map stays on screen.
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

    public MainViewModel(LiveMemory? live = null)
    {
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
        Hover = new HoverTip(title, string.Join(Environment.NewLine, lines));
    }

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
