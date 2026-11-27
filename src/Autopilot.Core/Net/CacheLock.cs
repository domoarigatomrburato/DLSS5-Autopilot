using Autopilot.Core.Storage;

namespace Autopilot.Core.Net;

/// <summary>
/// A cross-process lock on a cache entry: <c>&lt;entry&gt;.lock</c>, opened exclusively. The file is
/// left in place when released, since deleting a lock another process may be opening races.
/// </summary>
internal static class CacheLock
{
    private static readonly TimeSpan _wait = TimeSpan.FromMilliseconds(250);

    /// <summary>The lock of a cache entry or unpacked folder: <c>&lt;entry&gt;.lock</c>.</summary>
    public static string PathFor(string entry) => entry + ".lock";

    /// <summary>Waits, cancellably and without other limit, until no one else holds the lock, then holds it.</summary>
    public static async Task<FileStream> AcquireAsync(string lockPath, TimeProvider time, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException held) when (IoErrors.IsSharingViolation(held))
            {
                await Task.Delay(_wait, time, cancellationToken);
            }
        }
    }
}
