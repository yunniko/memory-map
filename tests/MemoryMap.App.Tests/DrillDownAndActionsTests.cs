using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MemoryMap.Core;
using static MemoryMap.App.Tests.Helpers.Samples;

namespace MemoryMap.App.Tests;

public class DrillDownAndActionsTests
{
    [AvaloniaFact]
    public void Clicking_an_app_zooms_to_its_processes_and_back_returns()
    {
        var (window, model) = OpenWith(new FakeActions());
        var chrome = window.Map.Scene.Tiles.Single(t => t.Node.Name == "chrome");

        Click(window, chrome.Rect);

        Assert.Equal("Apps/chrome.exe", model.ViewRoot?.Key);
        Assert.Equal(["chrome (10)", "chrome (11)"], window.Map.Scene.Tiles.Select(t => t.Node.Name).Order());
        Assert.Equal(["Memory", "Apps", "chrome"], model.Breadcrumb.Select(c => c.Name));
        Assert.True(window.BackButton.IsEnabled);
        Capture(window, "drill-chrome");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal("Apps", model.ViewRoot?.Key);
        var inMap = Center(window, window.Map.Scene.Tiles[0].Rect);
        window.MouseDown(inMap, MouseButton.XButton1);
        window.MouseUp(inMap, MouseButton.XButton1);
        Assert.Equal("root", model.ViewRoot?.Key);
        Assert.False(window.BackButton.IsEnabled);
    }

    [AvaloniaFact]
    public void A_breadcrumb_returns_to_its_level()
    {
        var (window, model) = OpenWith(new FakeActions());
        model.ZoomInto(model.Breakdown!.Root.Find("Apps/chrome.exe")!);
        Dispatcher.UIThread.RunJobs();

        var crumb = window.BreadcrumbList.GetRealizedContainers()
            .SelectMany(c => c.GetVisualDescendants().OfType<Button>())
            .Single(b => b.IsVisible && (b.Content as string) == "Memory");
        crumb.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("root", model.ViewRoot?.Key);
    }

    [AvaloniaFact]
    public void The_zoomed_view_survives_a_refresh_and_falls_back_when_the_app_exits()
    {
        var (_, model) = OpenWith(new FakeActions());
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
        var (window, model) = OpenWith(new FakeActions());
        var root = model.Breakdown!.Root;

        var appMenu = Headers(window.BuildMenu(root.Find("Apps/chrome.exe")!));
        Assert.Equal(["Zoom in", "End process: zoom in to pick one of its 2", "Open file location"], appMenu);
        Assert.False(MenuItems(window.BuildMenu(root.Find("Apps/chrome.exe")!))[1].IsEnabled);

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
        var (window, _) = OpenWith(new FakeActions());
        var code = window.Map.Scene.Tiles.Single(t => t.Node.Name == "code");
        var at = Center(window, code.Rect);

        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Right);
        window.MouseUp(at, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.Menu?.IsOpen);
        Assert.Equal(["Zoom in", "End code (PID 12)…", "Open file location"], Headers(window.Menu));
    }

    [AvaloniaFact]
    public void Ending_a_process_asks_first_and_does_nothing_on_cancel()
    {
        var actions = new FakeActions();
        var (_, model) = OpenWith(actions);
        string? asked = null;
        model.Confirm = (question, _) =>
        {
            asked = question;
            return Task.FromResult(false);
        };

        Wait(model.EndAsync(model.Breakdown!.Root.Find("Apps/code.exe")!));

        Assert.Equal("End code (PID 12)?", asked);
        Assert.Empty(actions.Ended);
        Assert.Null(model.Notice);
    }

    [AvaloniaFact]
    public void Ending_a_process_after_confirming_reports_the_outcome()
    {
        var actions = new FakeActions { Result = new(ActionOutcome.AccessDenied, "Windows denied access to end code (PID 12).") };
        var (window, model) = OpenWith(actions);
        model.Confirm = (_, _) => Task.FromResult(true);

        Wait(model.EndAsync(model.Breakdown!.Root.Find("Apps/code.exe")!));

        Assert.Equal([12], actions.Ended);
        Assert.True(window.NoticeBar.IsVisible);
        Assert.Equal("Windows denied access to end code (PID 12).", window.NoticeText.Text);
        Capture(window, "notice-denied");

        model.DismissNotice();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.NoticeBar.IsVisible);
    }

    [AvaloniaFact]
    public void An_app_with_several_processes_is_never_ended_whole()
    {
        var actions = new FakeActions();
        var (_, model) = OpenWith(actions);
        model.Confirm = (_, _) => Task.FromResult(true);

        Wait(model.EndAsync(model.Breakdown!.Root.Find("Apps/chrome.exe")!));

        Assert.Empty(actions.Ended);
    }

    [AvaloniaFact]
    public void The_confirm_dialog_defaults_to_cancel()
    {
        var owner = new Window();
        owner.Show();
        var dialog = new ConfirmDialog("End code (PID 12)?", "Anything unsaved is lost.", "End process");
        var answer = dialog.ShowDialog<bool>(owner);
        Dispatcher.UIThread.RunJobs();

        Assert.True(dialog.CancelButton.IsDefault);
        Assert.Equal("End process", dialog.ConfirmButton.Content);
        Capture(dialog, "confirm-end");

        dialog.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Wait(answer);
        Assert.False(answer.Result);
    }

    [AvaloniaFact]
    public void The_confirm_button_answers_yes()
    {
        var owner = new Window();
        owner.Show();
        var dialog = new ConfirmDialog("End code (PID 12)?", "Anything unsaved is lost.", "End process");
        var answer = dialog.ShowDialog<bool>(owner);
        Dispatcher.UIThread.RunJobs();

        dialog.ConfirmButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Wait(answer);

        Assert.True(answer.Result);
    }

    private static (MainWindow Window, MainViewModel Model) OpenWith(IProcessActions actions)
    {
        var model = new MainViewModel(actions: actions);
        model.Apply(Breakdown());
        var window = new MainWindow { DataContext = model, Width = 1180, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, model);
    }

    private static Point Center(MainWindow window, LayoutRect rect) =>
        window.Map.TranslatePoint(new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2)), window)!.Value;

    private static void Click(MainWindow window, LayoutRect rect)
    {
        var at = Center(window, rect);
        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<MenuItem> MenuItems(ContextMenu? menu) => [.. menu!.ItemsSource!.OfType<MenuItem>()];

    private static List<string> Headers(ContextMenu? menu) => [.. MenuItems(menu).Select(i => (string)i.Header!)];

    // Runs the UI thread until the task completes, so continuations posted back to it can run.
    private static void Wait(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Assert.True(task.IsCompleted, "the task did not finish");
        task.GetAwaiter().GetResult();
    }

    private sealed class FakeActions : IProcessActions
    {
        public List<int> Ended { get; } = [];

        public ActionResult Result { get; init; } = new(ActionOutcome.Done, "Ended.");

        public ActionResult End(ProcessSample process)
        {
            Ended.Add(process.Pid);
            return Result;
        }

        public ActionResult ShowInExplorer(ProcessSample process) => new(ActionOutcome.Done, "");
    }
}
