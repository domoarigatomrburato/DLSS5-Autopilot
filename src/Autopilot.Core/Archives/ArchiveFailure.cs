namespace Autopilot.Core.Archives;

/// <summary>Why an archive was refused.</summary>
public enum ArchiveFailure
{
    /// <summary>The file does not start with a zip, gzip or 7z signature.</summary>
    NotAnArchive,

    /// <summary>The archive is damaged or cut short.</summary>
    Corrupt,

    /// <summary>A member's name would escape the destination or is not a valid Windows name.</summary>
    UnsafeName,

    /// <summary>Two members share a path, ignoring case.</summary>
    DuplicateName,

    /// <summary>A tar holds a symbolic or hard link.</summary>
    Link,

    /// <summary>A tar holds an entry that is neither a file, a folder nor metadata, such as a device.</summary>
    UnsupportedEntry,

    /// <summary>More entries than the limit.</summary>
    TooManyEntries,

    /// <summary>The expanded size is over the limit, or a member yields more than its header declares.</summary>
    TooLarge,

    /// <summary>The archive expands more times its compressed size than the limit allows.</summary>
    RatioExceeded,

    /// <summary>No member ends with the requested ending.</summary>
    MemberNotFound,

    /// <summary>More than one member ends with the requested ending.</summary>
    AmbiguousMember,

    /// <summary>The destination already exists.</summary>
    DestinationExists,
}
