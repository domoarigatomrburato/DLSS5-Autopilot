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
/// <param name="logger">Where what was left, and why, is logged.</param>
internal sealed partial class InstallRollback(InstallDisk disk, ILogger logger)
{
    /// <summary>
    /// Rolls back the transaction <paramref name="record"/> journals in <paramref name="installFolder"/>: state
    /// <see cref="RecordState.Removing"/> saved first, then each path's entries undone in reverse journal order,
    /// leftover temporary files removed, and the folders it created removed when empty.
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

        Undoing undoing = new(record, new InstallPaths(gameFolder), installFolder, disk, logger);
        RollbackStop? stop = await undoing.EntriesAsync();
        await undoing.TemporaryFilesAsync();
        await undoing.FoldersAsync();
        if (stop is not null)
        {
            await disk.SaveRecordAsync(installFolder, undoing.Left, CancellationToken.None);
        }
        else if (record.Previous is { } previous)
        {
            await disk.SaveRecordAsync(installFolder, previous, CancellationToken.None);
        }
        else
        {
            await disk.DeleteRecordAsync(installFolder, CancellationToken.None);
        }

        return stop;
    }

    /// <summary>The rollback, as <see cref="RunAsync"/> does it; one that stopped on an error (the record could not be saved, a crash) is logged and given back as the stop.</summary>
    public static async Task<RollbackStop?> TryRunAsync(InstallDisk disk, ILogger logger, InstallRecord record, string gameFolder, string installFolder)
    {
        try
        {
            return await new InstallRollback(disk, logger).RunAsync(record, gameFolder, installFolder);
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

    /// <summary>One rollback's state: the record as it is being undone, and the previous install it returns to.</summary>
    private sealed class Undoing(InstallRecord record, InstallPaths paths, string installFolder, InstallDisk disk, ILogger logger)
    {
        private readonly PriorInstall _prior = PriorInstall.Before(record, installFolder, paths, logger);
        private readonly Dictionary<RecordEntry, UndoResult> _results = [with(ReferenceEqualityComparer.Instance)];

        /// <summary>The record left when the rollback could not undo everything: what remains, still <see cref="RecordState.Removing"/>.</summary>
        public InstallRecord Left => record with { Entries = [.. record.Entries.Select(Kept).OfType<RecordEntry>()] };

        public async Task<RollbackStop?> EntriesAsync()
        {
            EntryUndo undo = new(disk, paths, new OwnedFiles(record, paths.GameFolder));
            HashSet<string> undone = [with(StringComparer.OrdinalIgnoreCase)];
            RollbackStop? stop = null;
            foreach (RecordEntry entry in record.Entries.Reverse().Where(entry => undone.Add(RecordPaths.Key(entry.Path))))
            {
                RollbackStop? stopped = await PathAsync(undo, entry.Path);
                stop ??= stopped;
            }

            return stop;
        }

        // Every entry the record has at the path, undone together.
        private async Task<RollbackStop?> PathAsync(EntryUndo undo, string path)
        {
            string full = paths.Full(path);
            if (!paths.Contains(full))
            {
                InstallResolver.LogOutside(logger, path);
                return null;
            }

            PathEntries at = Undoable(PathEntries.At(record, path), _prior.At(full));
            bool restoresOriginal = at.Placed?.Original is { } original && original != _prior.At(full)?.Entry.Original;
            RollbackStop? stop = null;
            foreach (Undone done in await undo.UndoAsync(at, restoresOriginal))
            {
                _results[done.Entry] = done.Result;
                if (done.Result != UndoResult.Done)
                {
                    LogUndone(logger, done.Entry.Path, done.Result);
                }

                stop ??= done.Result is UndoResult.Stuck or UndoResult.RestoreFailed ? new(full, done.Error) : null;
            }

            return stop;
        }

        public async Task TemporaryFilesAsync()
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

        // The folders this install created, deepest first, each only when empty.
        public async Task FoldersAsync()
        {
            IEnumerable<string> created = record.CreatedFolders
                .Select(paths.Full)
                .Where(folder => !_prior.CreatedFolders.Contains(folder) && paths.Contains(folder))
                .OrderByDescending(folder => folder.Length);
            foreach (string folder in created.Where(folder => Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()))
            {
                await disk.DeleteFolderAsync(folder);
            }
        }

        // An entry carried over unchanged is the previous install's, which stays.
        private static PathEntries Undoable(PathEntries at, Prior? previous) =>
            at.Placed is { } placed && previous is not null && placed == previous.Entry with { Path = placed.Path } ? at with { Placed = null } : at;

        private RecordEntry? Kept(RecordEntry entry)
        {
            if (!_results.TryGetValue(entry, out UndoResult result))
            {
                return entry;
            }

            // A held file or an original put back keeps what it is: undoing it again is safe, and it never stands for the file at its path.
            if (!entry.IsPlaced)
            {
                return result is UndoResult.Stuck or UndoResult.RestoreFailed ? entry : null;
            }

            return result switch
            {
                UndoResult.Stuck => entry with { State = EntryState.Stuck },
                UndoResult.RestoreFailed => entry with { State = EntryState.RestoreFailed },
                _ => null,
            };
        }
    }
}

/// <summary>Where a rollback stopped.</summary>
/// <param name="Path">The file it could not remove or put back; null when an error stopped the whole rollback.</param>
/// <param name="Error">The error, as Windows or the run gave it; null when there was none (the place was taken).</param>
internal sealed record RollbackStop(string? Path, Exception? Error);
