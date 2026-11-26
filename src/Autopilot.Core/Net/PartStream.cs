namespace Autopilot.Core.Net;

/// <summary>
/// A download's <c>.part</c> file opened at an offset: everything after it is dropped, writes go to
/// the end, and closing flushes it to the disk, so the file moved into place is the file on disk.
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

    // NetClient closes each attempt's stream with await using.
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await FlushAsync();
            // There is no asynchronous fsync, so this blocks; it runs once per attempt.
            RandomAccess.FlushToDisk(SafeFileHandle);
        }
        finally
        {
            await base.DisposeAsync();
        }
    }
}
