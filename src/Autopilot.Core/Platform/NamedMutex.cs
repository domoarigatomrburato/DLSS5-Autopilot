namespace Autopilot.Core.Platform;

/// <summary>
/// A named mutex, the same name in every process (the stores hold one around a read-modify-write). It
/// belongs to the thread that took it, which must release it: hold it only around synchronous work.
/// </summary>
internal sealed class NamedMutex : IDisposable
{
    // Waited in slices, so a cancelled token stops the wait: on Unix a wait on a named mutex cannot
    // also wait on the token's handle.
    private static readonly TimeSpan _slice = TimeSpan.FromMilliseconds(100);

    private readonly Mutex _mutex;

    private NamedMutex(Mutex mutex) => _mutex = mutex;

    /// <summary>Opens (or creates) the mutex named <paramref name="name"/>, not held.</summary>
    /// <exception cref="UnauthorizedAccessException">It exists with an access list that refuses this process (an elevated one made it).</exception>
    /// <exception cref="WaitHandleCannotBeOpenedException">The name belongs to another kind of handle.</exception>
    /// <exception cref="IOException">The system could not open it.</exception>
    public static Mutex Open(string name) => new(false, name);

    /// <summary>
    /// Opens the mutex through <paramref name="open"/>, then waits, cancellably and without other
    /// limit, until no one else holds it, and holds it.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="UnauthorizedAccessException">The mutex could not be opened; see <see cref="Open"/> for the others.</exception>
    public static NamedMutex Hold(string name, Func<string, Mutex> open, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Mutex mutex = open(name);
        try
        {
            while (!TryTake(mutex, _slice))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            return new(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    /// <summary>Opens the mutex through <paramref name="open"/> and holds it when no one else does; null at once otherwise.</summary>
    /// <exception cref="UnauthorizedAccessException">The mutex could not be opened; see <see cref="Open"/> for the others.</exception>
    public static NamedMutex? TryHold(string name, Func<string, Mutex> open)
    {
        Mutex mutex = open(name);
        if (TryTake(mutex, TimeSpan.Zero))
        {
            return new(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    // A mutex its owner left without releasing (a crash) is ours now.
    private static bool TryTake(Mutex mutex, TimeSpan wait)
    {
        try
        {
            return mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
