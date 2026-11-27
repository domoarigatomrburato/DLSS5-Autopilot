using Autopilot.Core.Library;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// The refusals the folder decides, from read-only queries only: whether it is there and writable, whether the
/// game runs from it, whether a file the install changes is held, and whether its drive has the room.
/// </summary>
internal static class FolderRefusals
{
    /// <summary>
    /// Room the install record takes beside the bytes the install writes: its largest size, once more for the
    /// temporary file each atomic save writes beside it.
    /// </summary>
    private const long RecordMargin = 2L * InstallRecords.MaxBytes;

    /// <summary>
    /// Why the install folder cannot be written: missing, or not writable by the current user, worded for a store
    /// folder the Xbox app owns; null when it can. Asked of the folder, never by writing a probe.
    /// </summary>
    public static InstallBlocker? Access(IFolderAccess access, GameEntry game, string installFolder) => access.CanWrite(installFolder) switch
    {
        FolderAccess.Writable => null,
        FolderAccess.Missing => new(BlockerKind.FolderMissing, Texts.Format("RefusalFolderMissing", installFolder)),
        _ when game.InLockedStoreFolder => new(BlockerKind.StoreFolder, Texts.Format("RefusalStoreFolder", installFolder)),
        _ => new(BlockerKind.FolderDenied, Texts.Format("RefusalFolderDenied", installFolder)),
    };

    /// <summary>
    /// Why the install cannot go ahead while a program from <paramref name="gameFolder"/> or <paramref name="installFolder"/>
    /// runs: its files are open. Asked before anything is gathered and again just before the transaction.
    /// </summary>
    public static InstallBlocker? GameRunning(IProcesses processes, string gameFolder, string installFolder) =>
        RunningGames.Inside(processes.List(), [gameFolder, installFolder]) is [{ ImagePath: { } image }, ..]
            ? new(BlockerKind.GameRunning, Texts.Format("RefusalGameRunning", Path.GetFileName(image)))
            : null;

    /// <summary>The files <paramref name="resolved"/> overwrites, renames or deletes that another program holds now, each named.</summary>
    public static IEnumerable<InstallBlocker> Locked(ResolvedInstall resolved) =>
        FileLocks.Held(Touched(resolved))
            .Select(held => new InstallBlocker(BlockerKind.Locked, Texts.Format("RefusalLocked", resolved.Paths.Relative(held.Path))));

    /// <summary>
    /// Why the game's drive cannot take <paramref name="resolved"/>: every byte it writes, plus the record's margin.
    /// A file is written to a temporary name beside its own and renamed, so its bytes are counted once; backups are
    /// renames and cost nothing. Null when there is room, or when the drive's free space is not known.
    /// </summary>
    public static InstallBlocker? FreeSpace(IFreeSpace space, ResolvedInstall resolved)
    {
        long needed = RecordMargin + resolved.Changes
            .OfType<WriteFile>()
            .Sum(write => write.Facts.Size);
        DriveSpace drive = space.DriveOf(resolved.Paths.GameFolder);
        return drive.FreeBytes is { } free && free < needed
            ? new(BlockerKind.FreeSpace, Texts.Format("RefusalFreeSpace", drive.Name, DiskFullException.HumanSize(free), DiskFullException.HumanSize(needed)))
            : null;
    }

    // The existing files the install renames away or deletes: every file it overwrites is renamed first (backed up or held).
    private static IEnumerable<string> Touched(ResolvedInstall resolved) =>
        resolved.Changes
            .Select(change => change switch
            {
                MoveFile move => move.From,
                BackUpFile backup => backup.From,
                DeleteFile delete => delete.Path,
                _ => null,
            })
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
