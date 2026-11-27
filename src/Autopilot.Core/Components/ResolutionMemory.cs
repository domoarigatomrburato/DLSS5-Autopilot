namespace Autopilot.Core.Components;

/// <summary>
/// One resolution per key for the life of its owner, which a <see cref="ComponentRun"/> keeps for one
/// job: callers asking together share one request, a success is reused for the run, and a failure is
/// forgotten so the next ask tries again (a network failure is no answer to keep). A success
/// <paramref name="keep"/> turns down is handed to the callers that shared it and forgotten too.
/// The shared resolution belongs to no caller: it runs under the run's token, a cancelled one is
/// forgotten like a failure, and each caller stops waiting for it with its own token.
/// </summary>
/// <param name="run">The run's token, which the shared resolutions run under.</param>
/// <param name="keep">Whether a success is kept for the run; unset, every one is.</param>
internal sealed class ResolutionMemory<TKey, TValue>(CancellationToken run, Func<TValue, bool>? keep = null)
    where TKey : notnull
{
    private readonly Dictionary<TKey, Lazy<Task<TValue>>> _kept = [];
    private readonly Lock _lock = new();

    public async Task<TValue> GetAsync(TKey key, Func<CancellationToken, Task<TValue>> resolve, CancellationToken cancellationToken)
    {
        Lazy<Task<TValue>>? pending;
        lock (_lock)
        {
            if (!_kept.TryGetValue(key, out pending))
            {
                // Only stored here: the resolver starts outside the lock, on the first caller's Value.
                pending = new(() => resolve(run));
                _kept[key] = pending;
            }
        }

        TValue value;
        try
        {
            value = await pending.Value.WaitAsync(cancellationToken);
        }
        catch when (Failed(pending))
        {
            Forget(key, pending);
            throw;
        }

        if (keep?.Invoke(value) == false)
        {
            Forget(key, pending);
        }

        return value;
    }

    // The resolution itself failed (its start threw, or its task faulted or was canceled), not only this caller's wait.
    private static bool Failed(Lazy<Task<TValue>> pending) =>
        !pending.IsValueCreated || pending.Value.IsFaulted || pending.Value.IsCanceled;

    private void Forget(TKey key, Lazy<Task<TValue>> pending)
    {
        lock (_lock)
        {
            // Only this resolution: a later ask may already have started again.
            if (_kept.TryGetValue(key, out Lazy<Task<TValue>>? kept) && kept == pending)
            {
                _ = _kept.Remove(key);
            }
        }
    }
}
