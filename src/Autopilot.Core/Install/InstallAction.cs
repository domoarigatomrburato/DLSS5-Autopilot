namespace Autopilot.Core.Install;

/// <summary>
/// One concrete change at one path, as <see cref="InstallResolver"/> decided it from the plan, the disk and
/// the record: the entries it journals before its group runs, and the changes on disk it makes, in order.
/// The transaction makes exactly these changes; the preview shows them.
/// </summary>
/// <param name="Path">The path, relative to the game folder, <c>/</c> separated.</param>
internal abstract record InstallAction(string Path)
{
    /// <summary>The entries the record gains, as <see cref="EntryState.Pending"/>, <see cref="EntryState.Held"/> or <see cref="EntryState.Restored"/>, before the group's first change.</summary>
    public abstract IEnumerable<RecordEntry> Journal { get; }

    /// <summary>The changes on disk, in the order they are made.</summary>
    public abstract IReadOnlyList<DiskChange> Changes(InstallPaths paths);
}

/// <summary>A file written where nothing is.</summary>
/// <param name="Path">Where.</param>
/// <param name="Class">Whether it stays as written.</param>
/// <param name="Facts">Its hash and size.</param>
/// <param name="Source">Its bytes.</param>
/// <param name="Carried">The game's original the previous install kept in a backup behind this path, which stays; null when none.</param>
internal sealed record WriteNew(string Path, EntryClass Class, FileFacts Facts, FileSource Source, OriginalFile? Carried) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal => [new(Path, Class, EntryState.Pending, Facts, Carried)];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths) => [new WriteFile(paths.Full(Path), Facts, Source)];
}

/// <summary>
/// Our file of the previous install, held beside itself until commit, and the new one written in its place;
/// the game's original behind it, when there is one, keeps its backup.
/// </summary>
/// <param name="Path">Where.</param>
/// <param name="Class">Whether the new file stays as written.</param>
/// <param name="Facts">The new file's hash and size.</param>
/// <param name="Source">The new file's bytes.</param>
/// <param name="Previous">The previous install's entry for the path, relative to this install's game folder.</param>
internal sealed record ReplaceOurs(string Path, EntryClass Class, FileFacts Facts, FileSource Source, RecordEntry Previous) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal =>
    [
        Previous with { State = EntryState.Held, Original = null },
        new(Path, Class, EntryState.Pending, Facts, Previous.Original),
    ];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths)
    {
        string full = paths.Full(Path);
        return [new MoveFile(full, InstallPaths.HeldOf(full)), new WriteFile(full, Facts, Source)];
    }
}

/// <summary>The game's own file renamed to its backup name, then ours written in its place.</summary>
/// <param name="Path">Where.</param>
/// <param name="Class">Whether our file stays as written.</param>
/// <param name="Facts">Our file's hash and size.</param>
/// <param name="Source">Our file's bytes.</param>
/// <param name="Original">The game's file, as read before any change.</param>
internal sealed record BackUpThenWrite(string Path, EntryClass Class, FileFacts Facts, FileSource Source, OriginalFile Original) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal => [new(Path, Class, EntryState.Pending, Facts, Original)];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths)
    {
        string full = paths.Full(Path);
        return [new BackUpFile(full, InstallPaths.BackupOf(full, Original.Backup)), new WriteFile(full, Facts, Source)];
    }
}

/// <summary>A file that is not ours renamed out of the way, under its backup name.</summary>
/// <param name="Path">Where.</param>
/// <param name="Original">The file, as read before any change.</param>
internal sealed record MoveAsideFile(string Path, OriginalFile Original) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal => [new(Path, EntryClass.Component, EntryState.Pending, null, Original)];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths)
    {
        string full = paths.Full(Path);
        return [new BackUpFile(full, InstallPaths.BackupOf(full, Original.Backup))];
    }
}

/// <summary>
/// A file held beside itself until commit: our file of the previous install the new one no longer places, or
/// a file under a backup name the record does not vouch for, which the real backup is to take.
/// </summary>
/// <param name="Path">The file.</param>
/// <param name="Entry">Its held entry.</param>
internal sealed record HoldFile(string Path, RecordEntry Entry) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal => [Entry];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths)
    {
        string full = paths.Full(Path);
        return [new MoveFile(full, InstallPaths.HeldOf(full))];
    }

    /// <summary>Holds our file of the previous install, whose entry is <paramref name="previous"/>.</summary>
    public static HoldFile Ours(RecordEntry previous) => new(previous.Path, previous with { State = EntryState.Held, Original = null });

    /// <summary>Holds the untrusted file at <paramref name="path"/>, under a backup name.</summary>
    public static HoldFile Untrusted(string path) => new(path, new(path, EntryClass.Component, EntryState.Held, null, null));
}

/// <summary>
/// A game's original the previous install kept, renamed from its backup name: back under its path, when the new
/// install no longer places anything there, or to a superseded name, when the game has replaced the file since.
/// </summary>
/// <param name="Path">The file the original belongs to.</param>
/// <param name="Original">The original, as the previous install recorded it.</param>
/// <param name="Superseded">The superseded name's number it goes to; null when it goes back under the path.</param>
internal sealed record RestoreOriginal(string Path, OriginalFile Original, int? Superseded) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal => [new(Path, EntryClass.Component, EntryState.Restored, null, Original) { Superseded = Superseded }];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths)
    {
        string full = paths.Full(Path);
        string to = Superseded is { } number ? InstallPaths.SupersededOf(full, number) : full;
        return [new MoveFile(InstallPaths.BackupOf(full, Original.Backup), to)];
    }
}

/// <summary>A folder created, recorded so uninstall removes it when empty.</summary>
/// <param name="Path">The folder.</param>
internal sealed record MakeFolder(string Path) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal => [];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths) => [new CreateFolderChange(paths.Full(Path))];
}
