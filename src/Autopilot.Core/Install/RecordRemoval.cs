using Autopilot.Core.Resources;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Uninstall from the record: removes exactly what the record names, puts back what it replaced, and nothing
/// else. State <see cref="RecordState.Removing"/> is saved before the first change, and the record again after
/// each path that changed the disk, so a crash anywhere leaves a record the next run finishes from: idempotent
/// and resumable. The one path for a person's uninstall and for a removal an earlier run left part way; whether
/// the game runs is the caller's to check.
/// </summary>
/// <param name="disk">Makes the changes.</param>
/// <param name="outside">Undoes the changes outside the folder's files.</param>
/// <param name="logger">Where what was done is logged.</param>
internal sealed class RecordRemoval(InstallDisk disk, IOutsideChanges outside, ILogger logger)
{
    /// <summary>Uninstall's waits for an entry's file another program holds: four more tries, each after a longer wait, before it counts as stuck.</summary>
    private static readonly TimeSpan[] _growingWaits = [TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(1200), TimeSpan.FromMilliseconds(1600)];

    /// <summary>A disk for a removal a person waits on: an entry's file another program holds is tried with growing waits.</summary>
    public static InstallDisk NewDisk(InstallRecords records, IWriteSteps steps, TimeProvider time, DiskSeam seam) =>
        new(records, steps, time, seam, _growingWaits);

    /// <summary>
    /// Removes what <paramref name="record"/> (<see cref="RecordState.Installed"/>, or <see cref="RecordState.Removing"/>
    /// with no install under it) names: its entries, path by path, then the helper folder, the runtime logs, the
    /// folders it created, and the changes outside the files; the record goes last, when nothing is left.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="recordGameFolder">The game folder the reader derived from where the record sits.</param>
    /// <param name="installFolder">Where the record is.</param>
    /// <param name="cancellationToken">Stops between two paths; the rest stays on the record.</param>
    /// <exception cref="IOException">The record could not be saved, or a change failed other than by a held file.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled: what was not reached is on the record.</exception>
    public async Task<UninstallFinished> RunAsync(InstallRecord record, string recordGameFolder, string installFolder, CancellationToken cancellationToken)
    {
        installFolder = Path.GetFullPath(installFolder);
        if (record.State != RecordState.Removing)
        {
            record = record with { State = RecordState.Removing };
            await disk.SaveRecordAsync(installFolder, record, CancellationToken.None);
        }

        Removing removing = new(record, new InstallPaths(recordGameFolder), installFolder, disk, logger);
        await removing.EntriesAsync(cancellationToken);
        InstallRecord left = await outside.ConfigEditsUndoneAsync(removing.Current, removing.Paths);
        await removing.LeftoversAsync();
        left = await outside.SystemChangesUndoneAsync(left with { Entries = removing.Current.Entries });
        return await removing.FinishAsync(left);
    }

    /// <summary>One removal's state: the record as it is being emptied, and what the report says.</summary>
    private sealed class Removing(InstallRecord record, InstallPaths paths, string installFolder, InstallDisk disk, ILogger logger)
    {
        private readonly EntryWalk _walk = new(record);
        private readonly UninstallReport _report = new(logger);
        private int _changesSaved = disk.Changes;
        private bool _unsaved;

        public InstallPaths Paths => paths;

        public InstallRecord Current => _walk.Current;

        // Each path's entries undone together, last written first, the record saved after each that changed the disk.
        public Task EntriesAsync(CancellationToken cancellationToken) =>
            new EntryUndo(disk, paths, new OwnedFiles(record, paths.GameFolder))
                .WalkAsync(_walk, static (at, _) => new(at, RestoresOriginal: true), AfterPathAsync, Outside, logger, cancellationToken);

        public async Task LeftoversAsync()
        {
            RuntimeLeftovers leftovers = new(record, paths, installFolder, disk, _report);
            await leftovers.HelperFolderAsync();
            await leftovers.LogsAsync();
            await FolderCleanup.TemporaryFilesAsync(record, paths, installFolder, disk);
            await FolderCleanup.CreatedFoldersAsync(record, paths, disk, new HashSet<string>());
        }

        // The record goes only when nothing is left; otherwise it keeps what is, written only when it changed since saved.
        public async Task<UninstallFinished> FinishAsync(InstallRecord left)
        {
            bool outsideKept = OutsideChanges.Carries(left);
            if (left.Entries.Count == 0 && !outsideKept)
            {
                await disk.DeleteRecordAsync(installFolder, CancellationToken.None);
            }
            else if (_unsaved || OutsideUndone(left))
            {
                await disk.SaveRecordAsync(installFolder, left, CancellationToken.None);
            }

            foreach (RecordEntry entry in left.Entries)
            {
                _report.Leave(entry.Path);
            }

            return _report.Finished(outsideKept);
        }

        private bool OutsideUndone(InstallRecord left) =>
            left.ConfigEdits.Count != record.ConfigEdits.Count
            || left.GraphicsPreferences.Count != record.GraphicsPreferences.Count
            || left.Registrations.Count != record.Registrations.Count;

        // A path reaching outside the game folder is never followed: it leaves the record, which is saved by the next path or at the end.
        private void Outside(string path)
        {
            _report.Say(Texts.Format("UninstallLineOutside", path));
            _unsaved = true;
        }

        // A path that changed nothing on disk is not saved for: undone again after a crash, it changes nothing either.
        private async Task AfterPathAsync(string full, IReadOnlyList<Undone> undone)
        {
            foreach (Undone done in undone)
            {
                Report(done);
            }

            _unsaved = true;
            if (disk.Changes > _changesSaved)
            {
                await disk.SaveRecordAsync(installFolder, _walk.Current, CancellationToken.None);
                _changesSaved = disk.Changes;
                _unsaved = false;
            }
        }

        // A held file or an original put back during an open transaction is not an item of the install: only what is left of it is said.
        private void Report(Undone done)
        {
            string path = done.Entry.Path;
            if (!done.Entry.IsPlaced && done.IsSettled)
            {
                return;
            }

            (string? line, bool counts) = done.Result switch
            {
                UndoResult.Done => (Texts.Format("UninstallLineRemoved", path), true),
                UndoResult.Restored => (Texts.Format("UninstallLineRestored", path), true),
                UndoResult.OriginalGone => (Texts.Format("UninstallLineOriginalGone", path), true),
                UndoResult.LeftChanged => (Texts.Format("UninstallLineChanged", path), false),
                UndoResult.Superseded => (done.Note, false),
                UndoResult.Stuck => (Texts.Format("UninstallLineStuck", path), false),
                UndoResult.RestoreFailed => (Texts.Format("UninstallLineRestoreFailed", path), false),
                _ => (null, false),
            };
            _report.Say(line, counts);
        }
    }
}
