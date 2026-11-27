using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>One install's resolution and application, and on any failure its rollback and its note.</summary>
/// <param name="newDisk">Makes a disk for the changes.</param>
/// <param name="outside">Undoes the changes outside the files, as a rollback and a commit's release do.</param>
/// <param name="logger">Where the run is logged.</param>
/// <param name="failures">Keeps the note.</param>
/// <param name="places">The registry and the layer folder a plan's shared steps are resolved against; null when the transaction has none.</param>
internal sealed class InstallRun(Func<InstallDisk> newDisk, IOutsideWork outside, ILogger logger, Failures failures, SharedPlaces? places = null)
{
    /// <summary>Resolves and applies <paramref name="plan"/> over <paramref name="recorded"/>; on a failure, rolls back and keeps the note.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled: rolled back, and the note kept.</exception>
    public async Task<InstallOutcome> RunAsync(InstallPlan plan, RecordedInstall? recorded, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        TransactionApply apply = NewApply();
        try
        {
            ResolvedInstall resolved = await Resolver().ResolveAsync(plan, recorded, cancellationToken);
            return await apply.RunAsync(resolved, progress, cancellationToken);
        }
        catch (OperationCanceledException cancelled)
        {
            _ = await FailAfterAsync(cancelled, apply);
            throw;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return await FailAfterAsync(failure, apply);
        }
    }

    private TransactionApply NewApply()
    {
        InstallDisk disk = newDisk();
        return new(disk, failures.Subject.InstallFolder, logger, outside.For(disk, failures.Subject.InstallFolder));
    }

    private InstallResolver Resolver() => new(logger) { Shared = places };

    // The install failed: what its journal names is rolled back, and the reason kept.
    private async Task<InstallFailed> FailAfterAsync(Exception failure, TransactionApply apply)
    {
        FailureOutcome outcome = FailureOutcome.NothingWritten;
        if (apply.Saved is { } journal)
        {
            InstallDisk disk = newDisk();
            RollbackStop? stop = await InstallRollback.TryRunAsync(disk, outside.For(disk, failures.Subject.InstallFolder), logger, journal, failures.Subject.GameFolder, failures.Subject.InstallFolder);
            outcome = stop is not null ? FailureOutcome.RollbackIncomplete
                : apply.Changed ? FailureOutcome.RolledBack
                : FailureOutcome.NothingWritten;
        }

        return failures.Failed(FailureOperation.Install, failure, outcome);
    }
}
