using MemoryMap.Core;

namespace MemoryMap.App;

/// <summary>
/// One row of the Startup tab. Kept across reads (by entry id), so a refresh updates the row in
/// place instead of rebuilding the list under the pointer. <see cref="IsEnabled"/> is what the
/// switch shows: flipping it asks the owner to make the change, and the row then shows what
/// Windows reads back, so a refused or failed change springs back.
/// </summary>
public sealed class StartupItem : ObservableObject
{
    private readonly StartupViewModel _owner;
    private StartupEntry _entry;
    private StartupUsage _usage = StartupUsage.None;
    private bool _isEnabled;
    private bool _isBusy;

    internal StartupItem(StartupViewModel owner, StartupEntry entry)
    {
        _owner = owner;
        _entry = entry;
        _isEnabled = entry.Enabled;
    }

    public StartupEntry Entry => _entry;

    public string Name => _entry.Name;

    /// <summary>Publisher and where it is registered, for the second line.</summary>
    public string Detail => _entry.Publisher is { } publisher ? $"{publisher}  ·  {_entry.Location}" : _entry.Location;

    public string? Problem => _entry.Problem;

    public string Status => StartupText.Status(_entry, _usage);

    public string StatusHint => _entry.CountsProcesses ? _entry.Command : StartupText.NotCountedHint;

    public string Memory => StartupText.Memory(_entry, _usage);

    public IReadOnlyList<ProcessSample> Processes => _usage.Processes;

    /// <summary>The map leaves out apps holding no private memory, so those have nothing to show.</summary>
    public bool CanShowOnMap => _entry.CountsProcesses && _usage.PrivateWorkingSet > 0;

    public bool CanSwitch => !_isBusy && _owner.CanChange && _entry.CanSwitch;

    public string SwitchHint => StartupText.SwitchHint(_entry);

    /// <summary>True while a change to this entry is being made.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        internal set
        {
            if (Set(ref _isBusy, value))
            {
                Raise(nameof(CanSwitch));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (value == _isEnabled)
            {
                return;
            }
            if (!CanSwitch)
            {
                // The switch moved without a change being possible: put it back.
                Raise(nameof(IsEnabled));
                return;
            }
            _isEnabled = value;
            Raise(nameof(IsEnabled));
            _owner.Switch(this, value);
        }
    }

    internal void Update(StartupEntry entry)
    {
        var old = _entry;
        _entry = entry;
        // While this row's own change is in flight, a reload started by another row may predate
        // it; the switch keeps showing the change until this row's own reload.
        var enabled = _isBusy ? _isEnabled : entry.Enabled;
        if (old == entry && _isEnabled == enabled)
        {
            return;
        }
        _isEnabled = enabled;
        // Most fields follow from the entry; announce them all rather than diff each one.
        Raise("");
    }

    // Processes is read on demand (Show on map), not bound, so only the shown text is announced.
    internal void Update(StartupUsage usage)
    {
        var (status, memory, couldShow) = (Status, Memory, CanShowOnMap);
        _usage = usage;
        if (status != Status || memory != Memory)
        {
            Raise(nameof(Status));
            Raise(nameof(Memory));
        }
        if (couldShow != CanShowOnMap)
        {
            Raise(nameof(CanShowOnMap));
        }
    }
}

/// <summary>
/// The Startup tab: every program that starts at sign-in (read off the UI thread each time the
/// tab is opened or refreshed), what each costs in memory now (matched on every sample while
/// the tab is on screen), and the switch for each. Outcomes go to <see cref="Noticed"/>; "Show
/// on map" goes to <see cref="ShowOnMapRequested"/>. Without a reader (a system without
/// one, or a headless test that gives none) the tab says so and stays empty.
/// </summary>
public sealed class StartupViewModel : ObservableObject
{
    private readonly IStartupReader? _reader;
    private readonly IStartupActions? _actions;
    private readonly IProcessPaths? _paths;
    private Dictionary<string, StartupItem> _byId = new(StringComparer.Ordinal);
    private IReadOnlyList<StartupItem> _items = [];
    private IReadOnlyList<ProcessSample> _processes = [];
    private string _message = "";
    private bool _isLoading;
    private int _reads;
    private bool _isShown;
    private bool _isMatching;

    public StartupViewModel(IStartupReader? reader = null, IStartupActions? actions = null, IProcessPaths? paths = null)
    {
        _reader = reader;
        _actions = actions;
        _paths = paths;
        _message = reader is null ? "There is no startup list on this system." : "";
    }

    /// <summary>A sentence for the notice bar: the outcome of a change or a file-location request.</summary>
    public event Action<string>? Noticed;

    /// <summary>Asks the window to show the entry's running processes on the map.</summary>
    public event Action<StartupItem>? ShowOnMapRequested;

    public IReadOnlyList<StartupItem> Items { get => _items; private set => SetList(ref _items, value); }

    /// <summary>Below the list: sources that could not be read, or why there is no list.</summary>
    public string Message { get => _message; private set => Set(ref _message, value); }

    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }

    /// <summary>False without an implementation of the changes (a test, or an OS without one): switches stay put.</summary>
    public bool CanChange => _actions is not null;

    /// <summary>Set while the tab is on screen: opening it reads the list again, and only then
    /// is each sample matched to the entries.</summary>
    public bool IsShown
    {
        get => _isShown;
        set
        {
            if (Set(ref _isShown, value) && value)
            {
                Refresh();
            }
        }
    }

    /// <summary>The newest per-sample match in flight; tests wait on it.</summary>
    internal Task Matching { get; private set; } = Task.CompletedTask;

    /// <summary>The newest change or read in flight; tests wait on it.</summary>
    internal Task Change { get; private set; } = Task.CompletedTask;

    /// <summary>The Refresh button: reads the list again.</summary>
    public void Refresh() => Change = LoadAsync();

    /// <summary>The switch: makes the change in the background (see <see cref="SetEnabledAsync"/>).</summary>
    internal void Switch(StartupItem item, bool enabled) => Change = SetEnabledAsync(item, enabled);

    /// <summary>"Open file location"; only a failure is reported.</summary>
    public void Locate(StartupItem item) => Change = ShowFileLocationAsync(item);

    /// <summary>Reads the list again, off the UI thread, and matches it to the newest processes.</summary>
    public async Task LoadAsync()
    {
        if (_reader is not { } reader)
        {
            return;
        }
        // Reads can overlap (a refresh during a switch); only the newest is shown.
        var read = ++_reads;
        IsLoading = true;
        var processes = _processes;
        var paths = _paths;
        Action show;
        try
        {
            var (list, usage) = await Task.Run(() =>
            {
                var list = reader.Read();
                return (list, paths is null ? null : StartupMatching.Match(list.Entries, processes, paths));
            });
            show = () => Show(list, usage);
        }
        catch (Exception e)
        {
            show = () => Message = $"Could not read the startup list: {e.Message}";
        }
        if (read == _reads)
        {
            show();
            IsLoading = false;
        }
    }

    /// <summary>
    /// Takes every sample; credits each entry with its running processes only while the tab is
    /// shown. The match runs off the UI thread (it asks Windows for new processes' paths, several
    /// ms a sample); a sample arriving while one is in flight is skipped, not queued (D022).
    /// </summary>
    public void Update(IReadOnlyList<ProcessSample> processes)
    {
        _processes = processes;
        if (!IsShown || _paths is not { } paths || Items.Count == 0 || _isMatching)
        {
            return;
        }
        _isMatching = true;
        Matching = MatchAsync(Items, processes, paths);
    }

    private async Task MatchAsync(IReadOnlyList<StartupItem> items, IReadOnlyList<ProcessSample> processes, IProcessPaths paths)
    {
        try
        {
            StartupEntry[] entries = [.. items.Select(i => i.Entry)];
            var usage = await Task.Run(() => StartupMatching.Match(entries, processes, paths));
            // A read that finished meanwhile brought its own match; this one is for the old rows.
            if (ReferenceEquals(items, Items))
            {
                foreach (var item in items)
                {
                    item.Update(usage[item.Entry.Id]);
                }
            }
        }
        catch (Exception e)
        {
            Message = $"Could not match the startup list to running programs: {e.Message}";
        }
        finally
        {
            _isMatching = false;
        }
    }

    /// <summary>Makes the change, reports it, then reads the list again so the row shows what Windows now has.</summary>
    public async Task SetEnabledAsync(StartupItem item, bool enabled)
    {
        if (_actions is not { } actions)
        {
            return;
        }
        item.IsBusy = true;
        try
        {
            var entry = item.Entry;
            var result = await Task.Run(() => actions.SetEnabled(entry, enabled));
            Noticed?.Invoke(result.Message);
        }
        catch (Exception e)
        {
            Noticed?.Invoke(StartupResult.Failed(item.Entry, "switch", e.Message).Message);
        }
        finally
        {
            item.IsBusy = false;
        }
        await LoadAsync();
    }

    public async Task ShowFileLocationAsync(StartupItem item)
    {
        if (_actions is not { } actions)
        {
            return;
        }
        var entry = item.Entry;
        var result = await Task.Run(() => actions.ShowFileLocation(entry));
        if (!result.Succeeded)
        {
            Noticed?.Invoke(result.Message);
        }
    }

    public void ShowOnMap(StartupItem item)
    {
        if (item.CanShowOnMap)
        {
            ShowOnMapRequested?.Invoke(item);
        }
    }

    private void Show(StartupList list, IReadOnlyDictionary<string, StartupUsage>? usage)
    {
        var items = new List<StartupItem>(list.Entries.Count);
        foreach (var entry in list.Entries)
        {
            if (_byId.TryGetValue(entry.Id, out var item))
            {
                item.Update(entry);
            }
            else
            {
                item = new StartupItem(this, entry);
            }
            if (usage is not null)
            {
                item.Update(usage[entry.Id]);
            }
            items.Add(item);
        }
        // Ids are unique in a reader's list (the reader removes repeats); rows no longer listed are dropped.
        _byId = items.ToDictionary(i => i.Entry.Id, StringComparer.Ordinal);
        Items = items;
        var on = items.Count(i => i.Entry.Enabled);
        Message = (items.Count == 1 ? "1 program starts" : $"{items.Count} programs start")
            + $" when you sign in; {on} {(on == 1 ? "is" : "are")} switched on."
            + (list.Unreadable.Count == 0 ? "" : " Could not read: " + string.Join("; ", list.Unreadable));
    }
}
