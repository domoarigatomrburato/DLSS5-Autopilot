using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// The one backup mechanism, decided: what happens to whatever already sits under a backup name before a game's
/// file takes it, and what becomes of an entry of the previous install the new one no longer names. Read-only.
/// </summary>
/// <param name="paths">The game folder.</param>
/// <param name="prior">The previous install.</param>
/// <param name="logger">Where originals that cannot come back are logged.</param>
internal sealed partial class BackupDecisions(InstallPaths paths, PriorInstall prior, ILogger logger)
{
    private readonly SupersededNames _superseded = new();
    private readonly List<string> _notes = [];

    /// <summary>What the report says about backups: each one superseded.</summary>
    public IReadOnlyList<string> Notes => _notes;

    /// <summary>The game's file at <paramref name="full"/>, read as <paramref name="original"/>, backed up after its backup name is cleared, and <paramref name="place"/>'s file written in its place.</summary>
    public async Task<IReadOnlyList<InstallAction>> BackUpThenWriteAsync(PlaceFile place, string full, OriginalFile original, Prior? before, CancellationToken cancellationToken) =>
        [.. await ClearBackupNameAsync(full, before, original.Backup, cancellationToken), new BackUpThenWrite(paths.Relative(full), place.Class, place.Facts, place.Source, original)];

    /// <summary>
    /// Before the game's file at <paramref name="full"/> takes its <paramref name="kind"/> backup name: a valid
    /// backup of an older original (the game updated the file since) is superseded, and anything else under the
    /// name is not trusted and is held until commit.
    /// </summary>
    public async Task<IReadOnlyList<InstallAction>> ClearBackupNameAsync(string full, Prior? before, BackupKind kind, CancellationToken cancellationToken)
    {
        List<InstallAction> actions = [];
        BackupKind? superseded = null;
        if (await prior.IsBackupValidAsync(before, cancellationToken))
        {
            superseded = before!.Entry.Original!.Backup;
            actions.Add(Supersede(full, before.Entry.Original));
        }

        string backup = InstallPaths.BackupOf(full, kind);
        if (superseded != kind && File.Exists(backup))
        {
            actions.Add(HoldFile.Untrusted(paths.Relative(backup)));
        }

        return actions;
    }

    /// <summary>
    /// An entry of the previous install the new one does not name: our file, still ours, is held until commit;
    /// the game's original comes back from a valid backup where nothing else is; a file the game replaced since
    /// stays, its stale backup superseded.
    /// </summary>
    public async Task<IReadOnlyList<InstallAction>> ReleaseAsync(string full, Prior before, CancellationToken cancellationToken)
    {
        FileFacts? current = await FileFacts.OfAsync(full, cancellationToken);
        bool ours = prior.IsOurs(before, full, current);
        bool restorable = await prior.IsBackupValidAsync(before, cancellationToken);
        if (before.Entry.Original is not null && !restorable)
        {
            LogOriginalLost(logger, before.Entry.Path);
        }

        List<InstallAction> actions = ours ? [HoldFile.Ours(before.Entry)] : [];
        if (restorable)
        {
            actions.Add(ours || current is null ? new RestoreOriginal(before.Entry.Path, before.Entry.Original!, null) : Supersede(full, before.Entry.Original!));
        }

        return actions;
    }

    private RestoreOriginal Supersede(string full, OriginalFile original)
    {
        SupersededName name = _superseded.Pick(full, paths.Relative(full));
        _notes.Add(name.Note);
        return new(paths.Relative(full), original, name.Number);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The game's original behind {Path} cannot be put back: its backup is missing or changed")]
    private static partial void LogOriginalLost(ILogger logger, string path);
}
