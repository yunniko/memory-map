namespace MemoryMap.App;

/// <summary>
/// Whether the desktop shows a tray icon to bring a hidden window back. Windows always does; a
/// Linux desktop may not, and may gain or lose one while the app runs, so there it is asked again
/// at each close, off the UI thread (G-003 criterion 3, D031).
/// </summary>
public sealed class TrayHost : ObservableObject
{
    private readonly Func<bool>? _probe;
    private bool _shown;

    private TrayHost(bool shown, Func<bool>? probe)
    {
        _shown = shown;
        _probe = probe;
    }

    /// <summary>A tray that is always there: Windows' notification area.</summary>
    public static TrayHost Always() => new(true, null);

    /// <summary>No tray at all: closing quits (headless tests).</summary>
    public static TrayHost Never() => new(false, null);

    /// <summary>A tray the desktop is asked about; assumed there until the first answer.</summary>
    public static TrayHost Probed(Func<bool> probe) => new(true, probe);

    /// <summary>The latest answer.</summary>
    public bool Shown { get => _shown; private set => Set(ref _shown, value); }

    /// <summary>Asks again. A fixed answer completes at once; a probe runs on the thread pool and
    /// the answer is stored on the caller's thread.</summary>
    public async Task<bool> RefreshAsync()
    {
        if (_probe is not null)
        {
            Shown = await Task.Run(_probe);
        }
        return Shown;
    }
}
