using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MemoryMap.Core;
using static MemoryMap.App.Tests.Helpers.Samples;
using static MemoryMap.App.Tests.Helpers.Ui;

namespace MemoryMap.App.Tests;

public class DrillDownAndActionsTests
{
    [AvaloniaFact]
    public void Clicking_an_app_zooms_to_its_processes_and_back_returns()
    {
        var (window, model) = Open(Breakdown(), actions: new FakeActions());
        var chrome = window.Map.Scene.Tiles.Single(t => t.Node.Name == "chrome");

        Click(window, chrome.Rect);

        Assert.Equal("Apps/chrome.exe", model.ViewRoot?.Key);
        Assert.Equal(["chrome (10)", "chrome (11)"], window.Map.Scene.Tiles.Select(t => t.Node.Name).Order());
        Assert.Equal(["Memory", "Apps"], model.Ancestors.Select(c => c.Name));
        Assert.Equal("chrome", window.CurrentCrumb.Text);
        Assert.True(window.BackButton.IsEnabled);
        Capture(window, "drill-chrome");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal("Apps", model.ViewRoot?.Key);
        var inMap = Center(window, window.Map.Scene.Tiles[0].Rect);
        window.MouseDown(inMap, MouseButton.XButton1);
        window.MouseUp(inMap, MouseButton.XButton1);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("root", model.ViewRoot?.Key);
        Assert.False(window.BackButton.IsEnabled);
    }

    [AvaloniaFact]
    public void Back_gestures_do_nothing_on_the_table()
    {
        var (window, model) = Open(Breakdown(), actions: new FakeActions());
        model.ZoomInto(model.Breakdown!.Root.Find("Apps/chrome.exe")!);
        window.Views.SelectedIndex = MainViewModel.TableView;
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.Equal("Apps/chrome.exe", model.ViewRoot?.Key);
    }

    [AvaloniaFact]
    public void A_breadcrumb_returns_to_its_level()
    {
        var (window, model) = Open(Breakdown(), actions: new FakeActions());
        model.ZoomInto(model.Breakdown!.Root.Find("Apps/chrome.exe")!);
        Dispatcher.UIThread.RunJobs();

        var crumb = window.BreadcrumbList.GetRealizedContainers()
            .SelectMany(c => c.GetVisualDescendants().OfType<Button>())
            .Single(b => (b.Content as string) == "Memory");
        crumb.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("root", model.ViewRoot?.Key);
        Assert.Empty(model.Ancestors);
        Assert.Equal("Memory", window.CurrentCrumb.Text);
    }

    [AvaloniaFact]
    public void The_zoomed_view_survives_a_refresh_and_falls_back_when_the_app_exits()
    {
        var (_, model) = Open(Breakdown(), actions: new FakeActions());
        model.ZoomInto(model.Breakdown!.Root.Find("Apps/code.exe")!);

        model.Apply(Breakdown(codeMb: 1200));
        Assert.Equal("Apps/code.exe", model.ViewRoot?.Key);
        Assert.Equal(1200 * Mb, model.ViewRoot!.Bytes);

        model.Apply(Breakdown(codeMb: 0)); // code exited
        Assert.Equal("Apps", model.ViewRoot?.Key);
    }

    [AvaloniaFact]
    public void The_menu_offers_end_only_for_a_single_process()
    {
        var (window, model) = Open(Breakdown(), actions: new FakeActions());
        var root = model.Breakdown!.Root;

        var appMenu = window.BuildMenu(root.Find("Apps/chrome.exe")!);
        Assert.Equal(["Zoom in", "End process: zoom in to pick one of its 2", "Open file location"], Headers(appMenu));
        Assert.False(MenuItems(appMenu)[1].IsEnabled);

        Assert.Equal(["Zoom in", "End code (PID 12)…", "Open file location"], Headers(window.BuildMenu(root.Find("Apps/code.exe")!)));
        Assert.Null(window.BuildMenu(root.Find("Kernel/paged-pool")!)); // no commands, no menu
    }

    [AvaloniaFact]
    public void Without_process_actions_the_menu_only_navigates()
    {
        var (window, model) = Open(Breakdown());

        Assert.Equal(["Zoom in"], Headers(window.BuildMenu(model.Breakdown!.Root.Find("Apps/code.exe")!)));
    }

    [AvaloniaFact]
    public void A_right_click_opens_the_menu_for_the_node_under_the_pointer()
    {
        var (window, _) = Open(Breakdown(), actions: new FakeActions());
        var code = window.Map.Scene.Tiles.Single(t => t.Node.Name == "code");

        Click(window, code.Rect, MouseButton.Right);

        Assert.True(window.Menu?.IsOpen);
        Assert.Equal(["Zoom in", "End code (PID 12)…", "Open file location"], Headers(window.Menu));
    }

    [AvaloniaFact]
    public void Ending_a_process_asks_first_and_does_nothing_on_cancel()
    {
        var actions = new FakeActions();
        var (_, model) = Open(Breakdown(), actions: actions);
        ConfirmRequest? asked = null;
        model.Confirm = request =>
        {
            asked = request;
            return Task.FromResult(false);
        };

        Wait(model.RunAsync(EndEntry(model, "Apps/code.exe")));

        Assert.Equal("End code (PID 12)?", asked?.Question);
        Assert.Empty(actions.Ended);
        Assert.Null(model.Notice);
    }

    [AvaloniaFact]
    public void Ending_a_process_after_confirming_reports_the_outcome()
    {
        var actions = new FakeActions { EndResult = new(ActionOutcome.AccessDenied, "Windows denied access to end code (PID 12).") };
        var (window, model) = Open(Breakdown(), actions: actions);
        model.Confirm = _ => Task.FromResult(true);

        Wait(model.RunAsync(EndEntry(model, "Apps/code.exe")));

        Assert.Equal([12], actions.Ended);
        Assert.True(window.NoticeBar.IsVisible);
        Assert.Equal("Windows denied access to end code (PID 12).", window.NoticeText.Text);
        Capture(window, "notice-denied");

        window.DismissButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.NoticeBar.IsVisible);
    }

    [AvaloniaFact]
    public void An_action_that_throws_becomes_a_notice_instead_of_a_crash()
    {
        var actions = new FakeActions { Locate = _ => throw new InvalidOperationException("Explorer is gone.") };
        var (window, model) = Open(Breakdown(), actions: actions);
        var locate = model.MenuFor(model.Breakdown!.Root.Find("Apps/code.exe")!).Single(e => e.Header == "Open file location");

        Wait(model.RunAsync(locate));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Open file location failed: Explorer is gone.", window.NoticeText.Text);
    }

    [AvaloniaFact]
    public void Open_file_location_tries_the_next_process_when_one_has_exited()
    {
        var actions = new FakeActions
        {
            Locate = p => p.Pid == 10 ? ActionResult.Exited(p) : ActionResult.Shown(@"C:\chrome.exe"),
        };
        var (_, model) = Open(Breakdown(), actions: actions);

        Wait(model.ShowFileLocationAsync(model.Breakdown!.Root.Find("Apps/chrome.exe")!));

        Assert.Equal([10, 11], actions.Located);
        Assert.Null(model.Notice);
    }

    [AvaloniaFact]
    public void The_confirm_dialog_defaults_to_cancel()
    {
        var (dialog, answer) = ShowConfirm(ConfirmRequest.ToEnd(Proc(12, "code", 100)));

        Assert.True(dialog.CancelButton.IsDefault);
        Assert.Equal("End process", dialog.ConfirmButton.Content);
        Assert.Equal("End code (PID 12)?", dialog.Question.Text);
        Capture(dialog, "confirm-end");

        dialog.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Wait(answer);
        Assert.False(answer.Result);
    }

    [AvaloniaFact]
    public void The_confirm_button_answers_yes()
    {
        var (dialog, answer) = ShowConfirm(ConfirmRequest.ToEnd(Proc(12, "code", 100)));

        dialog.ConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Wait(answer);

        Assert.True(answer.Result);
    }

    private static MenuEntry EndEntry(MainViewModel model, string key) =>
        model.MenuFor(model.Breakdown!.Root.Find(key)!).Single(e => e.Header.StartsWith("End ", StringComparison.Ordinal));

    private static List<MenuItem> MenuItems(ContextMenu? menu) => [.. menu!.ItemsSource!.OfType<MenuItem>()];

    private static List<string> Headers(ContextMenu? menu) => [.. MenuItems(menu).Select(i => (string)i.Header!)];

    private sealed class FakeActions : IProcessActions
    {
        public List<int> Ended { get; } = [];

        public List<int> Located { get; } = [];

        public ActionResult EndResult { get; init; } = new(ActionOutcome.Done, "Ended.");

        public Func<ProcessSample, ActionResult> Locate { get; init; } = _ => new(ActionOutcome.Done, "");

        public ActionResult End(ProcessSample process)
        {
            Ended.Add(process.Pid);
            return EndResult;
        }

        public ActionResult ShowFileLocation(ProcessSample process)
        {
            Located.Add(process.Pid);
            return Locate(process);
        }
    }
}
