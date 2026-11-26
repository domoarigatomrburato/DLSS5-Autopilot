namespace Autopilot.Core.Net;

/// <summary>
/// A download's <c>.part</c> file opened at an offset: everything after it is dropped and writes go
/// to the end.
/// </summary>
internal sealed class PartStream : FileStream
{
    public PartStream(string path, long offset)
        : base(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous)
    {
        SetLength(offset);
        _ = Seek(0, SeekOrigin.End);
    }

    /// <summary>What <see cref="NetClient.StreamToAsync"/> calls to open the file at each attempt's offset.</summary>
    public static Func<long, Stream> Opener(string path) => offset => new PartStream(path, offset);

    /// <summary>
    /// Puts the finished file on the disk before it is moved into place. Once per download, not per
    /// attempt, so a failed or cancelled attempt does not wait for the disk.
    /// </summary>
    public static void FlushToDisk(string path)
    {
        using Microsoft.Win32.SafeHandles.SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Write);
        RandomAccess.FlushToDisk(handle);
    }
}
