using Autopilot.Core.Platform;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Takes an open install back out, from its record alone: idempotent and resumable, so a second run after
/// a crash during the first finishes the job. The folder ends as it was before the install: the previous
/// install's record saved back, or none. Never cancelled: stopping half way is what it repairs.
/// </summary>
/// <param name="disk">Makes the changes.</param>
/// <param name="outside">Undoes the changes outside the files of an install with none under it.</param>
/// <param name="logger">Where what was left, and why, is logged.</param>
internal sealed partial class InstallRollback(InstallDisk disk, IOutsideChanges outside, ILogger logger)
{
    /// <summary>
    /// Rolls back the transaction <paramref name="record"/> journals in <paramref name="installFolder"/>: state
    /// <see cref="RecordState.Removing"/> saved first, then each path's entries undone in reverse journal order,
    /// leftover temporary files removed, the folders it created removed when empty, and with no install under it,
    /// the changes outside the files undone: what could not be stays on the record.
    /// </summary>
    /// <returns>Where it stopped, when a file could not be removed or put back; null when everything was undone.</returns>
    /// <exception cref="IOException">The record could not be saved, or a change failed other than by a held file.</exception>
    public async Task<RollbackStop?> RunAsync(InstallRecord record, string gameFolder, string installFolder)
    {
        if (record.State != RecordState.Removing)
        {
            record = record with { State = RecordState.Removing };
            await disk.SaveRecordAsync(installFolder, record, CancellationToken.None);
        }

        InstallPaths paths = new(gameFolder);
        Undoing undoing = new(record, installFolder, paths, logger);
        EntryWalk walk = new(record);
        await UndoEntriesAsync(record, walk, paths, undoing);
        await FolderCleanup.TemporaryFilesAsync(record, paths, installFolder, disk);
        await FolderCleanup.CreatedFoldersAsync(record, paths, disk, undoing.PriorFolders);
        await FinishAsync(record, walk.Current, paths, installFolder, undoing.Stop);
        return undoing.Stop;
    }

    // A path outside the game folder is logged and stays on the record, never followed.
    private Task UndoEntriesAsync(InstallRecord record, EntryWalk walk, InstallPaths paths, Undoing undoing) =>
        new EntryUndo(disk, paths, new OwnedFiles(record, paths.GameFolder)).WalkAsync(walk, undoing.Prepare, undoing.AfterPathAsync, outside: null, logger, CancellationToken.None);

    // The previous install's record back when everything was undone, with what this one added outside the files taken
    // back out; else what is left, which an install with none
    // under it gives its changes outside the files up from first; the record goes when nothing is left.
    private async Task FinishAsync(InstallRecord record, InstallRecord left, InstallPaths paths, string installFolder, RollbackStop? stop)
    {
        if (stop is null && record.Previous is { } previous)
        {
            await disk.SaveRecordAsync(installFolder, await outside.PreviousBackAsync(record, previous), CancellationToken.None);
            return;
        }

        if (record.Previous is null)
        {
            left = await outside.SystemChangesUndoneAsync(await outside.ConfigEditsUndoneAsync(left, paths));
        }

        if (stop is not null || OutsideChanges.Carries(left))
        {
            await disk.SaveRecordAsync(installFolder, left, CancellationToken.None);
        }
        else
        {
            await disk.DeleteRecordAsync(installFolder, CancellationToken.None);
        }
    }

    /// <summary>The rollback, as <see cref="RunAsync"/> does it; one that stopped on an error (the record could not be saved, a crash) is logged and given back as the stop.</summary>
    public static async Task<RollbackStop?> TryRunAsync(InstallDisk disk, IOutsideChanges outside, ILogger logger, InstallRecord record, string gameFolder, string installFolder)
    {
        try
        {
            return await new InstallRollback(disk, outside, logger).RunAsync(record, gameFolder, installFolder);
        }
        catch (Exception stopped) when (stopped is not OperationCanceledException)
        {
            LogStopped(logger, stopped, installFolder);
            return new(null, stopped);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The rollback in {Folder} stopped")]
    private static partial void LogStopped(ILogger logger, Exception failure, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rolling back, {Path} was {Result}")]
    private static partial void LogUndone(ILogger logger, string path, UndoResult result);

    /// <summary>One rollback's choices per path, against the previous install it returns to, and where it stopped.</summary>
    private sealed class Undoing(InstallRecord record, string installFolder, InstallPaths paths, ILogger logger)
    {
        private readonly PriorInstall _prior = PriorInstall.Before(record, installFolder, paths, logger);

        /// <summary>The first file that could not be removed or put back; null while there is none.</summary>
        public RollbackStop? Stop { get; private set; }

        /// <summary>The folders the previous install created, which stay.</summary>
        public IReadOnlySet<string> PriorFolders => _prior.CreatedFolders;

        // An entry carried over unchanged is the previous install's, which stays; its original stays put too.
        public PathUndo Prepare(PathEntries at, string full)
        {
            Prior? previous = _prior.At(full);
            PathEntries undoable = at.Placed is { } placed && previous is not null && placed == previous.Entry with { Path = placed.Path } ? at with { Placed = null } : at;
            return new(undoable, undoable.Placed?.Original is { } original && original != previous?.Entry.Original);
        }

        public Task AfterPathAsync(string full, IReadOnlyList<Undone> undone)
        {
            foreach (Undone done in undone.Where(done => !done.IsPlain))
            {
                LogUndone(logger, done.Entry.Path, done.Result);
            }

            Stop ??= undone.FirstOrDefault(done => !done.IsSettled) is { } stuck ? new(full, stuck.Error) : null;
            return Task.CompletedTask;
        }
    }
}

/// <summary>What a rollback and an uninstall tidy after a record's entries: leftover temporary files, and the folders it created.</summary>
internal static class FolderCleanup
{
    /// <summary>Removes the atomic write's leftover temporary files in the folders <paramref name="record"/> touched.</summary>
    /// <exception cref="IOException">A file could not be removed.</exception>
    public static async Task TemporaryFilesAsync(InstallRecord record, InstallPaths paths, string installFolder, InstallDisk disk)
    {
        IEnumerable<string> folders = record.Entries
            .Select(entry => Path.GetDirectoryName(paths.Full(entry.Path))!)
            .Concat(record.CreatedFolders.Select(paths.Full))
            .Append(Path.GetFullPath(installFolder))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(paths.Contains);
        foreach (string temporary in folders.SelectMany(DrivePaths.Files).Where(file => AtomicFile.IsTemporary(Path.GetFileName(file))))
        {
            await disk.DeleteAsync(temporary, CancellationToken.None);
        }
    }

    /// <summary>Removes the folder at <paramref name="folder"/> when it is there and empty; anything in it keeps it.</summary>
    /// <exception cref="IOException">The folder could not be removed.</exception>
    public static async Task IfEmptyAsync(string folder, InstallDisk disk)
    {
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            await disk.DeleteFolderAsync(folder);
        }
    }

    /// <summary>Removes the folders <paramref name="record"/> created, deepest first, each only when empty, but for those of <paramref name="kept"/>.</summary>
    /// <exception cref="IOException">A folder could not be removed.</exception>
    public static async Task CreatedFoldersAsync(InstallRecord record, InstallPaths paths, InstallDisk disk, IReadOnlySet<string> kept)
    {
        IEnumerable<string> created = record.CreatedFolders
            .Select(paths.Full)
            .Where(folder => !kept.Contains(folder) && paths.Contains(folder))
            .OrderByDescending(folder => folder.Length);
        foreach (string folder in created.Where(folder => Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()))
        {
            await disk.DeleteFolderAsync(folder);
        }
    }
}

/// <summary>Where a rollback stopped.</summary>
/// <param name="Path">The file it could not remove or put back; null when an error stopped the whole rollback.</param>
/// <param name="Error">The error, as Windows or the run gave it; null when there was none (the place was taken).</param>
internal sealed record RollbackStop(string? Path, Exception? Error);
