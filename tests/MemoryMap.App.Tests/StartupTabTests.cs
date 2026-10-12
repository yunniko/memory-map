using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MemoryMap.Core;
using static MemoryMap.App.Tests.Helpers.Samples;
using static MemoryMap.App.Tests.Helpers.Ui;

namespace MemoryMap.App.Tests;

public class StartupTabTests
{
    private static StartupEntry Entry(string id, string name, StartupScope scope = StartupScope.User,
        StartupSource source = StartupSource.RunKey, bool enabled = true, string? exe = null, string? folder = null) =>
        new(id, name, "Vendor", exe ?? $@"C:\{name}\{name}.exe", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
            source, scope, enabled, exe ?? $@"C:\{name}\{name}.exe", folder);

    // Chrome's two processes (10, 11) and Code's (12) come from their own folders.
    private static readonly FakePaths Paths = new(new()
    {
        [10] = @"C:\Chrome\chrome.exe",
        [11] = @"C:\Chrome\chrome.exe",
        [12] = @"C:\Code\code.exe",
    });

    private static FakeReader Machine() => new(
    [
        Entry("run/user/Chrome", "Chrome", exe: @"C:\Chrome\chrome.exe", folder: @"C:\Chrome"),
        Entry("run/user/Tool", "Tool", enabled: false),
        Entry("run/machine/Driver", "Driver", StartupScope.Machine),
        Entry("task/Fake_1/Task", "Store app", source: StartupSource.StoreTask),
    ]);

    private static (MainWindow Window, MainViewModel Model) OpenTab(FakeReader reader, FakeStartupActions? actions = null)
    {
        var startup = new StartupViewModel(reader, actions ?? new FakeStartupActions(reader), Paths);
        var (window, model) = Open(Breakdown(), startup: startup);
        window.Views.SelectedIndex = MainViewModel.StartupView;
        Dispatcher.UIThread.RunJobs();
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();
        return (window, model);
    }

    private static StartupItem Item(MainViewModel model, string id) => model.Startup.Items.Single(i => i.Entry.Id == id);

    private static T InRow<T>(MainWindow window, string name, Func<T, bool>? which = null)
        where T : Control =>
        window.StartupList.GetRealizedContainers()
            .Single(c => c.DataContext is StartupItem item && item.Name == name)
            .GetVisualDescendants().OfType<T>().First(c => which?.Invoke(c) ?? true);

    [AvaloniaFact]
    public void Opening_the_tab_lists_every_entry_with_what_it_costs_now()
    {
        var reader = Machine();
        var (window, model) = OpenTab(reader);

        Assert.Equal(1, reader.Reads);
        Assert.Equal(["Chrome", "Tool", "Driver", "Store app"], model.Startup.Items.Select(i => i.Name));
        var chrome = Item(model, "run/user/Chrome");
        Assert.Equal("Running, 2 processes", chrome.Status);
        Assert.Equal(Format.Bytes(3400 * Mb), chrome.Memory);
        Assert.Equal("Not running", Item(model, "run/user/Tool").Status);
        Assert.Equal(4, window.StartupList.GetRealizedContainers().Count());
        Assert.Contains("4 programs start when you sign in; 3 are switched on", window.StartupMessage.Text);
        Capture(window, "startup-tab");

        // Samples keep the memory current (matched off the UI thread); the list itself is read
        // again only on open or Refresh.
        model.Apply(Breakdown(chromeMb: 1000));
        Wait(model.Startup.Matching);
        Assert.Equal(Format.Bytes(1400 * Mb), chrome.Memory);
        Assert.Equal(1, reader.Reads);
    }

    [AvaloniaFact]
    public void Samples_are_not_matched_while_another_tab_is_shown()
    {
        var reader = Machine();
        var (window, model) = OpenTab(reader);
        var chrome = Item(model, "run/user/Chrome");
        window.Views.SelectedIndex = MainViewModel.MapView;
        Dispatcher.UIThread.RunJobs();

        model.Apply(Breakdown(chromeMb: 1000));

        Assert.Equal(Format.Bytes(3400 * Mb), chrome.Memory);
        window.Views.SelectedIndex = MainViewModel.StartupView;
        Dispatcher.UIThread.RunJobs();
        Wait(model.Startup.Change);
        Assert.Equal(2, reader.Reads);
        Assert.Equal(Format.Bytes(1400 * Mb), chrome.Memory);
    }

    [AvaloniaFact]
    public void Switching_a_user_entry_off_and_on_changes_it_and_says_so()
    {
        var reader = Machine();
        var actions = new FakeStartupActions(reader);
        var (window, model) = OpenTab(reader, actions);
        var toggle = InRow<ToggleSwitch>(window, "Chrome");
        Assert.True(toggle.IsChecked);

        Click(window, toggle);
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("run/user/Chrome", false)], actions.Calls);
        Assert.False(Item(model, "run/user/Chrome").IsEnabled);
        Assert.False(InRow<ToggleSwitch>(window, "Chrome").IsChecked);
        Assert.Equal("Chrome off", model.Notice);

        Click(window, InRow<ToggleSwitch>(window, "Chrome"));
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("run/user/Chrome", false), ("run/user/Chrome", true)], actions.Calls);
        Assert.True(InRow<ToggleSwitch>(window, "Chrome").IsChecked);
    }

    [AvaloniaFact]
    public void A_reload_from_another_switch_does_not_undo_a_switch_still_being_made()
    {
        var reader = Machine();
        var actions = new FakeStartupActions(reader) { Held = "run/user/Tool" };
        var (window, model) = OpenTab(reader, actions);

        Click(window, InRow<ToggleSwitch>(window, "Tool"));
        var tool = model.Startup.Change;
        Click(window, InRow<ToggleSwitch>(window, "Chrome"));
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();

        // Chrome's reload read Tool as still off, but Tool's own change has not finished.
        Assert.False(Item(model, "run/user/Chrome").IsEnabled);
        Assert.True(InRow<ToggleSwitch>(window, "Tool").IsChecked);

        actions.Release.SetResult();
        Wait(tool);
        Dispatcher.UIThread.RunJobs();
        Assert.True(InRow<ToggleSwitch>(window, "Tool").IsChecked);
        Assert.Equal([("run/user/Chrome", false), ("run/user/Tool", true)], actions.Calls.Order());
    }

    [AvaloniaFact]
    public void The_count_reads_in_the_singular_and_stays_beside_a_source_that_could_not_be_read()
    {
        var reader = new FakeReader([Entry("run/user/Tool", "Tool")]) { Unreadable = ["HKLM Run: access denied"] };
        var (window, _) = OpenTab(reader);

        Assert.Equal("1 program starts when you sign in; 1 is switched on. Could not read: HKLM Run: access denied",
            window.StartupMessage.Text);
    }

    [AvaloniaFact]
    public void A_change_windows_did_not_keep_springs_back()
    {
        var reader = Machine();
        var actions = new FakeStartupActions(reader) { Fails = true };
        var (window, model) = OpenTab(reader, actions);

        Click(window, InRow<ToggleSwitch>(window, "Chrome"));
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(actions.Calls);
        Assert.True(Item(model, "run/user/Chrome").IsEnabled);
        Assert.True(InRow<ToggleSwitch>(window, "Chrome").IsChecked);
        Assert.Equal("failed", model.Notice);
    }

    [AvaloniaFact]
    public void Machine_and_store_switches_say_what_they_do_and_spring_back_when_refused()
    {
        var reader = Machine();
        // The administrator prompt is declined; the Store app is switched in Settings instead.
        var actions = new FakeStartupActions(reader) { Refuses = ["run/machine/Driver", "task/Fake_1/Task"] };
        var (window, model) = OpenTab(reader, actions);

        var driver = InRow<ToggleSwitch>(window, "Driver");
        var store = InRow<ToggleSwitch>(window, "Store app");
        Assert.True(driver.IsEnabled);
        Assert.True(store.IsEnabled);
        Assert.Contains("asks for administrator rights", ToolTip.GetTip(driver) as string);
        Assert.Contains("opens Windows Settings", ToolTip.GetTip(store) as string);

        Click(window, driver);
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();
        Click(window, store);
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("run/machine/Driver", false), ("task/Fake_1/Task", false)], actions.Calls);
        Assert.True(Item(model, "run/machine/Driver").IsEnabled);
        Assert.True(InRow<ToggleSwitch>(window, "Driver").IsChecked);
        Assert.True(InRow<ToggleSwitch>(window, "Store app").IsChecked);
        Assert.Equal("Store app refused", model.Notice);
    }

    [AvaloniaFact]
    public void Show_on_map_zooms_to_the_app_its_processes_belong_to()
    {
        var (window, model) = OpenTab(Machine());
        Assert.False(InRow<Button>(window, "Tool", b => b.Content as string == "Show on map").IsEnabled);

        Click(window, InRow<Button>(window, "Chrome", b => b.Content as string == "Show on map"));

        Assert.Equal(MainViewModel.MapView, model.ViewIndex);
        Assert.Equal("Apps/chrome.exe", model.ViewRoot?.Key);
        Assert.Equal(["chrome (10)", "chrome (11)"], window.Map.Scene.Tiles.Select(t => t.Node.Name).Order());
    }

    [AvaloniaFact]
    public void A_running_program_holding_no_memory_has_no_tile_to_show()
    {
        var reader = new FakeReader([Entry("run/user/Code", "Code", exe: @"C:\Code\code.exe", folder: @"C:\Code")]);
        var (window, model) = OpenTab(reader);
        model.Apply(Breakdown(codeMb: 0));
        Wait(model.Startup.Matching);
        Dispatcher.UIThread.RunJobs();

        var code = Item(model, "run/user/Code");
        Assert.Equal("Running", code.Status);
        Assert.False(code.CanShowOnMap);
        Assert.False(InRow<Button>(window, "Code", b => b.Content as string == "Show on map").IsEnabled);
    }

    [AvaloniaFact]
    public void Open_file_location_reports_only_a_failure()
    {
        var reader = Machine();
        var actions = new FakeStartupActions(reader);
        var (window, model) = OpenTab(reader, actions);

        Click(window, InRow<Button>(window, "Tool", b => b.Content as string == "Open file location"));
        Wait(model.Startup.Change);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["run/user/Tool"], actions.Located);
        Assert.Equal("Could not show Tool", model.Notice);
    }

    [AvaloniaFact]
    public void Without_a_reader_the_tab_says_why_it_is_empty()
    {
        var (window, model) = Open(Breakdown());
        window.Views.SelectedIndex = MainViewModel.StartupView;
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(model.Startup.Items);
        Assert.Contains("no startup list", window.StartupMessage.Text);
    }

    private sealed class FakePaths(Dictionary<int, string> byPid) : IProcessPaths
    {
        public string? PathOf(ProcessSample process) => byPid.GetValueOrDefault(process.Pid);

        public StringComparison Comparison => StringComparison.OrdinalIgnoreCase;
    }

    private sealed class FakeReader(IEnumerable<StartupEntry> entries) : IStartupReader
    {
        public List<StartupEntry> Entries { get; } = [.. entries];

        public int Reads { get; private set; }

        public IReadOnlyList<string> Unreadable { get; init; } = [];

        public StartupList Read()
        {
            Reads++;
            return new StartupList([.. Entries], Unreadable);
        }
    }

    /// <summary>Switches the fake reader's entries, as the real one changes what Windows reads back.</summary>
    private sealed class FakeStartupActions(FakeReader reader) : IStartupActions
    {
        public List<(string Id, bool Enabled)> Calls { get; } = [];

        public List<string> Located { get; } = [];

        public bool Fails { get; init; }

        /// <summary>Entries whose change is refused and leaves them as they were (a declined prompt, Settings).</summary>
        public string[] Refuses { get; init; } = [];

        /// <summary>An entry whose change waits for <see cref="Release"/>, as a slow write would.</summary>
        public string? Held { get; init; }

        public TaskCompletionSource Release { get; } = new();

        public ActionResult SetEnabled(StartupEntry entry, bool enabled)
        {
            if (entry.Id == Held)
            {
                Release.Task.Wait();
            }
            lock (Calls)
            {
                Calls.Add((entry.Id, enabled));
            }
            if (Fails)
            {
                return new(ActionOutcome.Failed, "failed");
            }
            if (Refuses.Contains(entry.Id))
            {
                return new(ActionOutcome.Refused, $"{entry.Name} refused");
            }
            var at = reader.Entries.FindIndex(e => e.Id == entry.Id);
            reader.Entries[at] = entry with { Enabled = enabled };
            return new(ActionOutcome.Done, $"{entry.Name} {(enabled ? "on" : "off")}");
        }

        public ActionResult ShowFileLocation(StartupEntry entry)
        {
            Located.Add(entry.Id);
            return new(ActionOutcome.Failed, $"Could not show {entry.Name}");
        }
    }
}
