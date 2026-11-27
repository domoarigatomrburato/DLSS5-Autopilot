using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Takes the entries one path has in a record back out, decided from the disk and the hashes: the one place a
/// rollback and an uninstall undo an entry. A file is deleted only while it is ours by hash; an original comes
/// back only from a backup holding exactly the recorded bytes, by rename, and a backup that cannot be put back
/// stays. Never cancelled: once started, an undo runs to the end.
/// </summary>
/// <param name="disk">Makes the changes.</param>
/// <param name="paths">The game folder the record's paths are relative to.</param>
/// <param name="owned">Which files are ours, by the record being undone.</param>
internal sealed class EntryUndo(InstallDisk disk, InstallPaths paths, OwnedFiles owned)
{
    private readonly SupersededNames _superseded = new();

    /// <summary>
    /// Undoes every path <paramref name="walk"/>'s record names, last written first, each path's entries together.
    /// A path leading outside the game folder is logged and never followed: given to <paramref name="outside"/> and
    /// dropped from the record, or left on it when there is no <paramref name="outside"/>.
    /// </summary>
    /// <param name="walk">The record, and what is left of it as each path is undone.</param>
    /// <param name="prepare">What to undo at a path (its entries, and whether the original comes back), given its entries and full path.</param>
    /// <param name="afterPath">Told each path's full path and what became of its entries, once <paramref name="walk"/> holds what is left of it.</param>
    /// <param name="outside">Told each path outside the game folder, dropped from the record; null keeps it there.</param>
    /// <param name="logger">Where a path outside the game folder is logged.</param>
    /// <param name="cancellationToken">Stops between two paths.</param>
    /// <exception cref="IOException">A file could not be read, or a change failed other than by another program holding it.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled: the paths not reached are left as they are.</exception>
    public async Task WalkAsync(EntryWalk walk, Func<PathEntries, string, PathUndo> prepare, Func<string, IReadOnlyList<Undone>, Task> afterPath, Action<string>? outside, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(walk);
        foreach (PathEntries at in walk.Paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string full = paths.Full(at.Path);
            if (!paths.Contains(full))
            {
                InstallResolver.LogOutside(logger, at.Path);
                if (outside is not null)
                {
                    walk.Drop(at.Path);
                    outside(at.Path);
                }

                continue;
            }

            PathUndo undo = prepare(at, full);
            IReadOnlyList<Undone> undone = await UndoAsync(undo.At, undo.RestoresOriginal);
            walk.Settle(undone);
            await afterPath(full, undone);
        }
    }

    /// <summary>
    /// Undoes what <paramref name="at"/> names, in this order: the file placed at the path deleted (and, when
    /// <paramref name="restoresOriginal"/>, the game's original renamed back from its backup); the original put
    /// back there returned to its backup name; our file held beside it renamed back. A placed file whose held
    /// file never left is the held file itself, never moved: it stays.
    /// </summary>
    /// <exception cref="IOException">A file could not be read, or a change failed other than by another program holding it.</exception>
    public async Task<IReadOnlyList<Undone>> UndoAsync(PathEntries at, bool restoresOriginal)
    {
        ArgumentNullException.ThrowIfNull(at);
        string full = paths.Full(at.Path);
        List<Undone> undone = [];
        if (at.Placed is { } placed)
        {
            bool neverHeld = at.Held is not null && !File.Exists(InstallPaths.HeldOf(full));
            undone.Add(neverHeld ? new(placed, UndoResult.Done) : await TryAsync(placed, () => PlacedAsync(placed, full, restoresOriginal)));
        }

        if (at.Restored is { } restored)
        {
            undone.Add(await TryAsync(restored, () => RestoredAsync(restored, full)));
        }

        if (at.Held is { } held)
        {
            undone.Add(await TryAsync(held, () => UnholdAsync(held, full)));
        }

        return undone;
    }

    // Another program holding a file stops that entry only.
    private static async Task<Undone> TryAsync(RecordEntry entry, Func<Task<Undone>> undo)
    {
        try
        {
            return await undo();
        }
        catch (FileHeldException held)
        {
            return new(entry, UndoResult.Stuck, held);
        }
    }

    private async Task<Undone> PlacedAsync(RecordEntry entry, string full, bool restoresOriginal)
    {
        if (restoresOriginal && entry.Original is not null)
        {
            return await RestoreAsync(entry, full);
        }

        FileFacts? current = await FileFacts.OfAsync(full, CancellationToken.None);
        if (current is null)
        {
            return new(entry, UndoResult.AlreadyGone);
        }

        if (!owned.IsOurs(full, current))
        {
            return new(entry, UndoResult.LeftChanged);
        }

        await disk.DeleteEntryAsync(full);
        return new(entry, UndoResult.Done);
    }

    private async Task<Undone> RestoreAsync(RecordEntry entry, string full)
    {
        string backup = InstallPaths.BackupOf(full, entry.Original!.Backup);
        FileFacts? current = await FileFacts.OfAsync(full, CancellationToken.None);
        if (!await owned.IsBackupValidAsync(entry, CancellationToken.None))
        {
            return await WithoutBackupAsync(entry, full, backup, current);
        }

        if (current is not null)
        {
            bool ours = owned.IsOurs(full, current);
            if (!ours && entry.Original.Facts.Matches(current))
            {
                // The store put the original back: the backup is redundant.
                await disk.DeleteEntryAsync(backup);
                return new(entry, UndoResult.Restored);
            }

            if (!ours)
            {
                // The game shipped a newer file: it stays, and the old original is set aside.
                SupersededName superseded = _superseded.Pick(full, entry.Path);
                await disk.MoveAsync(backup, superseded.Full, CancellationToken.None);
                return new(entry, UndoResult.Superseded) { Note = superseded.Note };
            }

            // The backup holds the recorded bytes: it takes the path over our file in one step.
            await disk.ReplaceAsync(backup, full);
            return new(entry, UndoResult.Restored);
        }

        return await PutBackAsync(entry, backup, full, UndoResult.Restored);
    }

    // Undone again after a crash, each of these ends as it did the first time.
    private async Task<Undone> WithoutBackupAsync(RecordEntry entry, string full, string backup, FileFacts? current)
    {
        if (entry.Original!.Facts.Matches(current))
        {
            // Never moved, or already put back: the original is where it was.
            return new(entry, UndoResult.Restored);
        }

        if (File.Exists(backup))
        {
            return new(entry, UndoResult.RestoreFailed);
        }

        if (current is null)
        {
            // Nothing of ours, and no original: what is left is for the game's store to repair.
            return new(entry, UndoResult.OriginalGone);
        }

        if (owned.IsOurs(full, current))
        {
            await disk.DeleteEntryAsync(full);
            return new(entry, UndoResult.OriginalGone);
        }

        // The game's newer file in place and the old original under a superseded name: set aside before.
        return await SupersededNames.HoldingAsync(full, entry.Path, name => OwnedFiles.IsOriginalAsync(entry, name, CancellationToken.None)) is { } superseded
            ? new(entry, UndoResult.Superseded) { Note = superseded.Note }
            : new(entry, UndoResult.RestoreFailed);
    }

    // An original put back, or superseded, returns to its backup name; one never moved, or already back, stays.
    private async Task<Undone> RestoredAsync(RecordEntry entry, string full)
    {
        if (entry.Original is not { } original)
        {
            return new(entry, UndoResult.Done);
        }

        string backup = InstallPaths.BackupOf(full, original.Backup);
        string from = entry.Superseded is { } number ? InstallPaths.SupersededOf(full, number) : full;
        if (File.Exists(backup) || !File.Exists(from))
        {
            return new(entry, UndoResult.Done);
        }

        if (!await original.Facts.IsAtAsync(from, CancellationToken.None))
        {
            return new(entry, UndoResult.LeftChanged);
        }

        await disk.MoveAsync(from, backup, CancellationToken.None);
        return new(entry, UndoResult.Done);
    }

    private async Task<Undone> UnholdAsync(RecordEntry entry, string full)
    {
        string held = InstallPaths.HeldOf(full);
        if (!File.Exists(held))
        {
            return new(entry, UndoResult.Done);
        }

        return File.Exists(full) ? new(entry, UndoResult.RestoreFailed) : await PutBackAsync(entry, held, full, UndoResult.Done);
    }

    private async Task<Undone> PutBackAsync(RecordEntry entry, string from, string full, UndoResult done)
    {
        try
        {
            await disk.MoveAsync(from, full, CancellationToken.None);
            return new(entry, done);
        }
        catch (FileHeldException held)
        {
            return new(entry, UndoResult.RestoreFailed, held);
        }
    }
}

/// <summary>The entries one path has in a record: the one standing for the file there, the original put back there, and the file held beside it.</summary>
/// <param name="Path">The path, as recorded.</param>
/// <param name="Placed">The entry standing for the file at the path; null when none does.</param>
/// <param name="Restored">The original put back at the path, or superseded from its backup; null when none was.</param>
/// <param name="Held">The file held beside the path; null when none is.</param>
internal sealed record PathEntries(string Path, RecordEntry? Placed, RecordEntry? Restored, RecordEntry? Held)
{
    /// <summary>The entries <paramref name="record"/> has at <paramref name="path"/>, compared ignoring case and slash.</summary>
    public static PathEntries At(InstallRecord record, string path)
    {
        string key = RecordPaths.Key(path);
        return Of(path, record.Entries.Where(entry => string.Equals(RecordPaths.Key(entry.Path), key, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The entries at <paramref name="path"/> among <paramref name="entries"/>, all of which name it.</summary>
    public static PathEntries Of(string path, IEnumerable<RecordEntry> entries)
    {
        IReadOnlyList<RecordEntry> at = [.. entries];
        return new(path, at.FirstOrDefault(entry => entry.IsPlaced), at.FirstOrDefault(entry => entry.State == EntryState.Restored), at.FirstOrDefault(HeldFiles.IsHeld));
    }
}

/// <summary>What to undo at one path.</summary>
/// <param name="At">Its entries to undo.</param>
/// <param name="RestoresOriginal">Whether the game's original comes back from its backup when the placed file goes.</param>
internal readonly record struct PathUndo(PathEntries At, bool RestoresOriginal);

/// <summary>A record as its paths are undone: each entry undone is replaced by what stays of it on the record.</summary>
/// <param name="record">The record.</param>
internal sealed class EntryWalk(InstallRecord record)
{
    private readonly Dictionary<RecordEntry, RecordEntry?> _settled = [with(ReferenceEqualityComparer.Instance)];
    private readonly HashSet<string> _dropped = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Each path's entries, grouped once, the path written last first.</summary>
    public IEnumerable<PathEntries> Paths
    {
        get
        {
            ILookup<string, RecordEntry> byKey = record.Entries.ToLookup(entry => RecordPaths.Key(entry.Path), StringComparer.OrdinalIgnoreCase);
            HashSet<string> walked = [with(StringComparer.OrdinalIgnoreCase)];
            return record.Entries.Reverse()
                .Where(entry => walked.Add(RecordPaths.Key(entry.Path)))
                .Select(first => PathEntries.Of(first.Path, byKey[RecordPaths.Key(first.Path)]));
        }
    }

    /// <summary>The record with what is left of each entry undone so far, in journal order; the others as they were.</summary>
    public InstallRecord Current => record with
    {
        Entries = [.. record.Entries.Where(entry => !_dropped.Contains(RecordPaths.Key(entry.Path))).Select(entry => _settled.TryGetValue(entry, out RecordEntry? kept) ? kept : entry).OfType<RecordEntry>()],
    };

    /// <summary>Drops every entry at <paramref name="path"/> from the record, undone or not.</summary>
    public void Drop(string path) => _dropped.Add(RecordPaths.Key(path));

    /// <summary>Takes what became of <paramref name="undone"/> onto the record.</summary>
    public void Settle(IEnumerable<Undone> undone)
    {
        foreach (Undone done in undone)
        {
            _settled[done.Entry] = done.Kept;
        }
    }
}

/// <summary>What became of an entry undone.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="Result">What became of it.</param>
/// <param name="Error">The error Windows gave when another program held a file; null otherwise.</param>
internal sealed record Undone(RecordEntry Entry, UndoResult Result, FileHeldException? Error = null)
{
    /// <summary>For <see cref="UndoResult.Superseded"/>, the report's line naming where the old original went; null otherwise.</summary>
    public string? Note { get; init; }

    /// <summary>Whether nothing of the entry is left to do, whatever became of its file.</summary>
    public bool IsSettled => Result is not (UndoResult.Stuck or UndoResult.RestoreFailed);

    /// <summary>
    /// The entry that stays on the record for the next run, null when it is settled: a held file or an original
    /// put back stays as it is (undoing it again is safe, and it never stands for the file at its path); a placed
    /// file is marked stuck or not restored.
    /// </summary>
    public RecordEntry? Kept => IsSettled ? null
        : !Entry.IsPlaced ? Entry
        : Entry with { State = Result == UndoResult.Stuck ? EntryState.Stuck : EntryState.RestoreFailed };

    /// <summary>Whether it ended in one of the plain ways a rollback does not log: taken out, put back, or already gone.</summary>
    public bool IsPlain => Result is UndoResult.Done or UndoResult.Restored or UndoResult.AlreadyGone;
}

/// <summary>What became of an entry undone.</summary>
internal enum UndoResult
{
    /// <summary>Taken back out: nothing of it is left to do.</summary>
    Done,

    /// <summary>The game's original is at the path again: put back from its backup, or already there.</summary>
    Restored,

    /// <summary>Nothing was at the path any more (antivirus took our file, say): there was nothing to take out.</summary>
    AlreadyGone,

    /// <summary>Our file had changed since (the game or a person replaced it): left where it is.</summary>
    LeftChanged,

    /// <summary>The game had shipped a newer file: it stays, and the old original's backup was set aside under a superseded name.</summary>
    Superseded,

    /// <summary>Our file was removed, or was gone already, but the original's backup was missing: the game's store can repair it.</summary>
    OriginalGone,

    /// <summary>A file could not be removed or renamed back: another program holds it.</summary>
    Stuck,

    /// <summary>The original could not be put back; its backup, if any, stays.</summary>
    RestoreFailed,
}
