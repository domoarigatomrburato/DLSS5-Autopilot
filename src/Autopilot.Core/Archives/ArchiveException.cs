namespace Autopilot.Core.Archives;

/// <summary>An archive the extractor refused, with the reason and a message naming the archive and member.</summary>
public sealed class ArchiveException(ArchiveFailure reason, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public ArchiveFailure Reason { get; } = reason;

    internal static ArchiveException For(ArchiveFailure reason, string archivePath, string detail, Exception? innerException = null) =>
        new(reason, $"{Path.GetFileName(archivePath)}: {detail}", innerException);
}
