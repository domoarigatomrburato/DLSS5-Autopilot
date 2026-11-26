namespace Autopilot.Core.Archives;

/// <summary>An archive the extractor refused, with the reason and a message naming the archive and member.</summary>
public sealed class ArchiveException : Exception
{
    public ArchiveException()
    {
    }

    public ArchiveException(string message)
        : base(message)
    {
    }

    public ArchiveException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ArchiveException(ArchiveFailure reason, string message, Exception? innerException = null)
        : base(message, innerException) => Reason = reason;

    public ArchiveFailure Reason { get; }

    internal static ArchiveException For(ArchiveFailure reason, string archivePath, string detail, Exception? innerException = null) =>
        new(reason, $"{Path.GetFileName(archivePath)}: {detail}", innerException);
}
