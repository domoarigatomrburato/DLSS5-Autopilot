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
            return new(entry, UndoResult.Done);
        }

        if (!owned.IsOurs(full, current))
        {
            return new(entry, UndoResult.LeftChanged);
        }

        await disk.DeleteAsync(full, CancellationToken.None);
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
                await disk.DeleteAsync(backup, CancellationToken.None);
                return new(entry, UndoResult.Done);
            }

            if (!ours)
            {
                // The game shipped a newer file: it stays, and the old original is set aside.
                await disk.MoveAsync(backup, _superseded.Pick(full, entry.Path).Full, CancellationToken.None);
                return new(entry, UndoResult.Superseded);
            }

            await disk.DeleteAsync(full, CancellationToken.None);
        }

        return await PutBackAsync(entry, backup, full);
    }

    private async Task<Undone> WithoutBackupAsync(RecordEntry entry, string full, string backup, FileFacts? current)
    {
        if (entry.Original!.Facts.Matches(current))
        {
            // Never moved: the original is where it was.
            return new(entry, UndoResult.Done);
        }

        if (current is not null && !File.Exists(backup) && owned.IsOurs(full, current))
        {
            await disk.DeleteAsync(full, CancellationToken.None);
            return new(entry, UndoResult.OriginalGone);
        }

        return new(entry, UndoResult.RestoreFailed);
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

        return File.Exists(full) ? new(entry, UndoResult.RestoreFailed) : await PutBackAsync(entry, held, full);
    }

    private async Task<Undone> PutBackAsync(RecordEntry entry, string from, string full)
    {
        try
        {
            await disk.MoveAsync(from, full, CancellationToken.None);
            return new(entry, UndoResult.Done);
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
        RecordEntry? Find(Func<RecordEntry, bool> kind) =>
            record.Entries.FirstOrDefault(entry => kind(entry) && string.Equals(RecordPaths.Key(entry.Path), key, StringComparison.OrdinalIgnoreCase));
        return new(path, Find(entry => entry.IsPlaced), Find(entry => entry.State == EntryState.Restored), Find(HeldFiles.IsHeld));
    }
}

/// <summary>What became of an entry undone.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="Result">What became of it.</param>
/// <param name="Error">The error Windows gave when another program held a file; null otherwise.</param>
internal sealed record Undone(RecordEntry Entry, UndoResult Result, FileHeldException? Error = null);

/// <summary>What became of an entry undone.</summary>
internal enum UndoResult
{
    /// <summary>Taken back out: nothing of it is left to do.</summary>
    Done,

    /// <summary>Our file had changed since (the game or a person replaced it): left where it is.</summary>
    LeftChanged,

    /// <summary>The game had shipped a newer file: it stays, and the old original's backup was set aside under a superseded name.</summary>
    Superseded,

    /// <summary>Our file was removed, but the original's backup was missing: the game's store can repair it.</summary>
    OriginalGone,

    /// <summary>A file could not be removed or renamed back: another program holds it.</summary>
    Stuck,

    /// <summary>The original could not be put back; its backup, if any, stays.</summary>
    RestoreFailed,
}
