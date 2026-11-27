using Autopilot.Core.Resources;

namespace Autopilot.Core.Archives;

/// <summary>
/// An archive the extractor refused. <see cref="Exception.Message"/> is a short English line for the
/// log; <see cref="Explain"/> gives the words a person reads, naming the archive and the member.
/// </summary>
public sealed class ArchiveException(ArchiveFailure reason, string archiveName, string? member, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public ArchiveFailure Reason { get; } = reason;

    /// <summary>The archive's file name.</summary>
    public string ArchiveName { get; } = archiveName;

    /// <summary>
    /// The member at fault, as the archive names it, when there is one: the refused or duplicated
    /// path, the ending asked for that matched none or several, the member past a limit.
    /// </summary>
    public string? Member { get; } = member;

    /// <summary>What happened to which archive, and what to do, from the resources.</summary>
    public string Explain()
    {
        // A limit can be passed by one member or by the whole archive; each has its own text.
        string id = "Archive" + Reason + (Member is not null && Reason is ArchiveFailure.TooLarge or ArchiveFailure.RatioExceeded ? "Member" : "");
        return Texts.Format(id, ArchiveName, Member);
    }

    internal static ArchiveException For(ArchiveFailure reason, string archivePath, string detail, Exception? innerException = null) =>
        For(reason, archivePath, member: null, detail, innerException);

    internal static ArchiveException For(ArchiveFailure reason, string archivePath, string? member, string detail, Exception? innerException = null)
    {
        string name = Path.GetFileName(archivePath);
        return new(reason, name, member, $"{name}: {detail}", innerException);
    }
}
