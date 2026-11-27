namespace Autopilot.Core.State;

/// <summary>Something a store could not do as asked, for the app to show once (M7).</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Folder">The game folder whose state it concerns.</param>
/// <param name="Text">The words the person reads.</param>
public sealed record StoreNotice(StoreNoticeKind Kind, string Folder, string Text);

/// <summary>What a store could not do; each is said once per file per process.</summary>
public enum StoreNoticeKind
{
    /// <summary>The file was written by a newer version: read where it can be, never written.</summary>
    NewerVersion,

    /// <summary>The file did not parse, or is not an object: moved aside to <c>&lt;name&gt;.corrupt</c>, and the store starts from defaults.</summary>
    Damaged,

    /// <summary>The file could not be read for a while (locked by another program): the last good copy is used, and the file is not written.</summary>
    Unreadable,

    /// <summary>A write found the drive full: the value is kept in memory.</summary>
    DiskFull,

    /// <summary>A write failed for another reason (access denied): the value is kept in memory.</summary>
    NotSaved,
}
