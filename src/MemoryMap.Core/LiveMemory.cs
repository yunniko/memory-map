using System.Diagnostics;

namespace MemoryMap.Core;

/// <summary>
/// Samples on a timer and publishes each breakdown. A failed sample (a counter that is briefly
/// invalid after resume, say) is reported through <see cref="Failed"/> and the loop carries on;
/// <see cref="Latest"/> keeps the last good breakdown, so a view never goes blank over one bad tick.
/// Events are raised on a thread-pool thread; UI code marshals them itself.
/// </summary>
public sealed class LiveMemory : IDisposable
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(250);

    private readonly IMemorySampler _sampler;
    private readonly BreakdownOptions _options;
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private Task? _loop;
    private bool _disposed;

    public LiveMemory(IMemorySampler sampler, TimeSpan interval, TimeProvider? clock = null, BreakdownOptions? options = null)
    {
        Validate(interval);
        _sampler = sampler;
        _options = options ?? new BreakdownOptions();
        _timer = new PeriodicTimer(interval, clock ?? TimeProvider.System);
    }

    public event Action<MemoryBreakdown>? Updated;

    public event Action<Exception>? Failed;

    public MemoryBreakdown? Latest { get; private set; }


    public TimeSpan Interval
    {
        get => _timer.Period;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Validate(value);
            _timer.Period = value;
        }
    }

    /// <summary>Starts the loop; the first sample is taken at once.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _loop ??= Task.Run(RunAsync);
    }

    /// <summary>
    /// Takes one sample now and publishes it. Returns false if it failed; the error goes to
    /// <see cref="Failed"/>. Safe to call while the loop runs: samples are taken one at a time,
    /// so <see cref="Latest"/> is always the newest.
    /// </summary>
    public bool RefreshNow()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            MemoryBreakdown breakdown;
            try
            {
                breakdown = Breakdown.Build(_sampler.Sample(), _options);
            }
            catch (Exception e) when (e is not ObjectDisposedException)
            {
                Failed?.Invoke(e);
                return false;
            }
            Latest = breakdown;
            Updated?.Invoke(breakdown);
            return true;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            Tick();
            while (await _timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                Tick();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
            // The sampler was disposed under a tick that was already running: shutting down.
        }
    }

    // A subscriber that throws is a bug in the subscriber; it must not stop the sampling for everyone.
    private void Tick()
    {
        try
        {
            RefreshNow();
        }
        catch (Exception e) when (e is not ObjectDisposedException)
        {
            Trace.TraceError($"LiveMemory: a subscriber threw: {e}");
        }
    }

    private static void Validate(TimeSpan interval)
    {
        if (interval < MinimumInterval)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, $"The refresh interval must be at least {MinimumInterval.TotalMilliseconds} ms.");
        }
    }

    /// <summary>Stops the loop and waits for a tick in progress. The sampler belongs to the caller.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _stop.Cancel();
        _timer.Dispose();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        lock (_gate)
        {
            _disposed = true;
        }
        _stop.Dispose();
    }
}
