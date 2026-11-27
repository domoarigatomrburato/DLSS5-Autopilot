namespace Autopilot.Core.Tests.Storage;

/// <summary>A clock whose timers fire at once, keeping how long each was set for, so a wait is asserted without waiting.</summary>
/// <remarks>Not FakeTimeProvider: the write before the wait is real file I/O, so a test cannot tell when the timer exists to advance past it.</remarks>
internal sealed class RecordingTimers : TimeProvider
{
    private readonly List<TimeSpan> _waits = [];

    /// <summary>The due time of each timer created, in order.</summary>
    public TimeSpan[] Waits
    {
        get
        {
            lock (_waits)
            {
                return [.. _waits];
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_waits)
        {
            _waits.Add(dueTime);
        }
        // Fired on the pool, never inside this call: the caller has not stored the timer yet.
        _ = ThreadPool.UnsafeQueueUserWorkItem(_ => callback(state), null);
        return new Fired();
    }

    private sealed class Fired : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
