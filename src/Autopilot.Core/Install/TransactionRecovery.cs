using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Finishes a transaction an earlier run left open in a folder (power loss, a killed process, a forced close)
/// before anything else happens there: an install or a removal part way is rolled back from its record, and a
/// commit stopped between its two saves has its held files deleted.
/// </summary>
/// <param name="newDisk">Makes a disk for the changes.</param>
/// <param name="installFolder">Where the record is.</param>
/// <param name="failures">Keeps the note when the folder cannot be put right.</param>
/// <param name="logger">Where the recovery is logged.</param>
internal sealed partial class TransactionRecovery(Func<InstallDisk> newDisk, string installFolder, Failures failures, ILogger logger)
{
    /// <summary>Finishes what <paramref name="recorded"/> left open.</summary>
    public async Task<Recovery> RunAsync(RecordedInstall? recorded)
    {
        if (recorded is { State: InstallState.Installing or InstallState.Removing, Record: { } open, GameFolder: { } folder })
        {
            LogRecovering(logger, installFolder);
            return new(true, await RollBackAsync(open, folder));
        }

        if (recorded is { State: InstallState.Installed, Record: { } done, GameFolder: { } doneFolder } && done.Entries.Any(HeldFiles.IsHeld))
        {
            LogRecovering(logger, installFolder);
            return new(true, await FinishCommitAsync(done, new InstallPaths(doneFolder)));
        }

        return Recovery.None;
    }

    private async Task<InstallFailed?> RollBackAsync(InstallRecord open, string gameFolder) =>
        await InstallRollback.TryRunAsync(newDisk(), logger, open, gameFolder, installFolder) is { } stop
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The install record in {Folder} was left open by an earlier run: finishing it first")]
    private static partial void LogRecovering(ILogger logger, string folder);
}

/// <summary>What finishing an open transaction came to.</summary>
/// <param name="Ran">Whether the folder held one, which was finished first.</param>
/// <param name="Failed">The failure when it could not be finished, after which nothing more is written; null otherwise.</param>
internal sealed record Recovery(bool Ran, InstallFailed? Failed)
{
    /// <summary>Nothing was open.</summary>
    public static Recovery None { get; } = new(false, null);
}
