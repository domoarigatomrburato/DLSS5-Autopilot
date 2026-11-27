using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Finishes a transaction an earlier run left open in a folder (power loss, a killed process, a forced close)
/// before anything else happens there: an install part way, or a rollback over an earlier install, is rolled
/// back from its record; a removal part way (an uninstall, or the rollback of a fresh install) is finished by
/// the uninstall's own path; and a commit stopped between its two saves has its held files deleted.
/// </summary>
/// <param name="newDisk">Makes a disk for the changes.</param>
/// <param name="installFolder">Where the record is.</param>
/// <param name="failures">Keeps the note when the folder cannot be put right.</param>
/// <param name="outside">Undoes the changes outside the files, as a rollback does.</param>
/// <param name="removal">Finishes a removal part way.</param>
/// <param name="gameRuns">Whether the game runs now, which leaves a removal part way for later; the caller checks it.</param>
/// <param name="logger">Where the recovery is logged.</param>
internal sealed partial class TransactionRecovery(Func<InstallDisk> newDisk, string installFolder, Failures failures, IOutsideChanges outside, RecordRemoval removal, Func<bool> gameRuns, ILogger logger)
{
    /// <summary>Finishes what <paramref name="recorded"/> left open.</summary>
    /// <param name="recorded">The folder's record as read.</param>
    /// <param name="cancellationToken">Stops a removal part way between two paths; nothing else is ever cancelled.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled during a removal: what was not reached is on the record.</exception>
    public async Task<Recovery> RunAsync(RecordedInstall? recorded, CancellationToken cancellationToken)
    {
        if (recorded is { State: InstallState.Removing, Record: { Previous: null } removing, GameFolder: { } removingFolder })
        {
            LogRecovering(logger, installFolder);
            return await FinishRemovalAsync(removing, removingFolder, cancellationToken);
        }

        if (recorded is { State: InstallState.Installing or InstallState.Removing, Record: { } open, GameFolder: { } folder })
        {
            LogRecovering(logger, installFolder);
            return new(RecoveryKind.Rollback, await RollBackAsync(open, folder));
        }

        if (recorded is { State: InstallState.Installed, Record: { } done, GameFolder: { } doneFolder } && done.Entries.Any(HeldFiles.IsHeld))
        {
            LogRecovering(logger, installFolder);
            return new(RecoveryKind.Commit, await FinishCommitAsync(done, new InstallPaths(doneFolder)));
        }

        return Recovery.None;
    }

    // A removal part way is finished as an uninstall would; with the game running or leftovers, the record stays for the next run.
    // No note is kept here: a rollback that stopped kept its own, and an uninstall reports through its outcome.
    private async Task<Recovery> FinishRemovalAsync(InstallRecord removing, string recordGameFolder, CancellationToken cancellationToken)
    {
        if (gameRuns())
        {
            LogRemovalLeft(logger, installFolder);
            return new(RecoveryKind.Removal, null);
        }

        try
        {
            UninstallFinished removed = await removal.RunAsync(removing, recordGameFolder, installFolder, cancellationToken);
            if (!removed.Complete)
            {
                LogRemovalLeft(logger, installFolder);
            }

            return new(RecoveryKind.Removal, null) { Removal = removed };
        }
        catch (Exception stopped) when (stopped is not OperationCanceledException)
        {
            LogRemovalStopped(logger, stopped, installFolder);
            return new(RecoveryKind.Removal, null) { RemovalStopped = stopped };
        }
    }

    private async Task<InstallFailed?> RollBackAsync(InstallRecord open, string gameFolder) =>
        await InstallRollback.TryRunAsync(newDisk(), outside, logger, open, gameFolder, installFolder) is { } stop
            ? failures.Failed(FailureOperation.Rollback, stop, FailureOutcome.RollbackIncomplete)
            : null;

    // A commit an earlier run left between its two saves: its held files go before anything else.
    private async Task<InstallFailed?> FinishCommitAsync(InstallRecord done, InstallPaths paths)
    {
        try
        {
            FileHeldException? kept = await HeldFiles.DeleteAsync(done, paths, newDisk(), installFolder, logger);
            return kept is null ? null : failures.Failed(FailureOperation.Install, kept, FailureOutcome.NothingWritten);
        }
        catch (Exception stopped) when (stopped is not OperationCanceledException)
        {
            return failures.Failed(FailureOperation.Install, stopped, FailureOutcome.NothingWritten);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The removal in {Folder} could not be finished (the game runs, or something is left): its record stays for the next uninstall")]
    private static partial void LogRemovalLeft(ILogger logger, string folder);

    [LoggerMessage(Level = LogLevel.Error, Message = "Finishing the removal in {Folder} stopped: its record stays for the next uninstall")]
    private static partial void LogRemovalStopped(ILogger logger, Exception failure, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The install record in {Folder} was left open by an earlier run: finishing it first")]
    private static partial void LogRecovering(ILogger logger, string folder);
}

/// <summary>What finishing an open transaction came to.</summary>
/// <param name="Kind">Which transaction the folder held, finished first.</param>
/// <param name="Failed">The failure when a rollback or a commit could not be finished, after which nothing more is written; null otherwise.</param>
internal sealed record Recovery(RecoveryKind Kind, InstallFailed? Failed)
{
    /// <summary>Nothing was open.</summary>
    public static Recovery None { get; } = new(RecoveryKind.None, null);

    /// <summary>Whether the folder held a transaction, which was finished first.</summary>
    public bool Ran => Kind != RecoveryKind.None;

    /// <summary>For a removal part way, what finishing it came to; null when it did not run (the game runs) or stopped.</summary>
    public UninstallFinished? Removal { get; init; }

    /// <summary>For a removal part way, the error that stopped it; null otherwise.</summary>
    public Exception? RemovalStopped { get; init; }

    /// <summary>
    /// What stops an install after it: the failure, or a refusal when a removal part way could not be finished
    /// (nothing may be installed over it); null when the install may go on.
    /// </summary>
    public InstallOutcome? Blocking =>
        (InstallOutcome?)Failed ?? (Kind == RecoveryKind.Removal && Removal is not { Complete: true } ? new InstallRefused(InstallRefusal.RemovalUnfinished) : null);
}

/// <summary>Which transaction an earlier run left open.</summary>
internal enum RecoveryKind
{
    /// <summary>None.</summary>
    None,

    /// <summary>An install part way, or a rollback over an earlier install: rolled back.</summary>
    Rollback,

    /// <summary>A removal part way, an uninstall or the rollback of a fresh install: finished by the uninstall's path.</summary>
    Removal,

    /// <summary>A commit stopped between its two saves: its held files deleted.</summary>
    Commit,
}
