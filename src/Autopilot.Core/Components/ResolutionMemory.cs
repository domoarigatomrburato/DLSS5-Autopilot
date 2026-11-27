namespace Autopilot.Core.Components;

/// <summary>
/// One resolution per key for the life of its owner, which the composition root keeps for the
/// process: callers asking together share one request, a success is reused for the run, and a
/// failure is forgotten so the next ask tries again (a network failure is no answer to keep).
/// </summary>
internal sealed class ResolutionMemory<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, Task<TValue>> _kept = [];
    private readonly Lock _lock = new();

    public async Task<TValue> GetAsync(TKey key, Func<CancellationToken, Task<TValue>> resolve, CancellationToken cancellationToken)
    {
        Task<TValue>? pending;
        lock (_lock)
        {
            if (!_kept.TryGetValue(key, out pending))
            {
                pending = resolve(cancellationToken);
                _kept[key] = pending;
            }
        }

        try
        {
            return await pending.WaitAsync(cancellationToken);
        }
        catch when (pending.IsFaulted || pending.IsCanceled)
        {
            lock (_lock)
            {
                // Only this failure: a later ask may already have started again.
                if (_kept.TryGetValue(key, out Task<TValue>? kept) && kept == pending)
                {
                    _ = _kept.Remove(key);
                }
            }

            throw;
        }
    }
}
