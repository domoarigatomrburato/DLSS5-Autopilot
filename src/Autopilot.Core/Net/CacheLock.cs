namespace Autopilot.Core.Net;

/// <summary>
/// A cross-process lock on a cache entry: <c>&lt;entry&gt;.lock</c>, opened exclusively. The file is
/// left in place when released, since deleting a lock another process may be opening races.
/// </summary>
internal static class CacheLock
{
    private static readonly TimeSpan _wait = TimeSpan.FromMilliseconds(250);

    // Windows: ERROR_SHARING_VIOLATION. Unix: .NET's FileShare.None is an flock, and a held one
    // surfaces as EWOULDBLOCK with the raw errno in HResult (11 on Linux, 35 on macOS and BSD).
    private static readonly int _sharingViolation = OperatingSystem.IsWindows() ? unchecked((int)0x80070020)
        : OperatingSystem.IsLinux() || OperatingSystem.IsAndroid() ? 11
        : 35;

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
            catch (IOException held) when (held.HResult == _sharingViolation)
            {
                await Task.Delay(_wait, time, cancellationToken);
            }
        }
    }
}
