using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using MemoryMap.Core;

namespace MemoryMap.App.Tests.Helpers;

/// <summary>Pointer input on map tiles and controls, dialogs and waiting, shared by the window specs.</summary>
internal static class Ui
{
    /// <summary>The centre of a tile, in window coordinates.</summary>
    public static Point Center(MainWindow window, LayoutRect rect) =>
        window.Map.TranslatePoint(new Point(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2)), window)!.Value;

    public static void Hover(MainWindow window, LayoutRect rect)
    {
        window.MouseMove(Center(window, rect));
        Dispatcher.UIThread.RunJobs();
    }

    public static void Click(MainWindow window, LayoutRect rect, MouseButton button = MouseButton.Left) =>
        Click(window, Center(window, rect), button);

    /// <summary>Clicks the middle of a control, as a pointer would.</summary>
    public static void Click(MainWindow window, Control control, MouseButton button = MouseButton.Left) =>
        Click(window, control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value, button);

    private static void Click(MainWindow window, Point at, MouseButton button)
    {
        window.MouseMove(at);
        window.MouseDown(at, button);
        window.MouseUp(at, button);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Shows a confirm dialog over a fresh owner window; the task is its answer.</summary>
    public static (ConfirmDialog Dialog, Task<bool> Answer) ShowConfirm(ConfirmRequest request)
    {
        var owner = new Window();
        owner.Show();
        var dialog = new ConfirmDialog(request);
        var answer = dialog.ShowDialog<bool>(owner);
        Dispatcher.UIThread.RunJobs();
        return (dialog, answer);
    }

    /// <summary>Runs the UI thread until the task completes, so continuations posted back to it can run.</summary>
    public static void Wait(Task task)
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
}
