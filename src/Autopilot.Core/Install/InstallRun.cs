using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>One install's resolution and application, and on any failure its rollback and its note.</summary>
/// <param name="newDisk">Makes a disk for the changes.</param>
/// <param name="outside">Undoes the changes outside the files, as a rollback does.</param>
/// <param name="logger">Where the run is logged.</param>
/// <param name="failures">Keeps the note.</param>
internal sealed class InstallRun(Func<InstallDisk> newDisk, IOutsideChanges outside, ILogger logger, Failures failures)
{
    /// <summary>Resolves and applies <paramref name="plan"/> over <paramref name="recorded"/>; on a failure, rolls back and keeps the note.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled: rolled back, and the note kept.</exception>
    public async Task<InstallOutcome> RunAsync(InstallPlan plan, RecordedInstall? recorded, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        TransactionApply apply = new(newDisk(), failures.Subject.InstallFolder, logger);
        try
        {
            ResolvedInstall resolved = await new InstallResolver(logger).ResolveAsync(plan, recorded, cancellationToken);
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

    // The install failed: what its journal names is rolled back, and the reason kept.
    private async Task<InstallFailed> FailAfterAsync(Exception failure, TransactionApply apply)
    {
        FailureOutcome outcome = FailureOutcome.NothingWritten;
        if (apply.Saved is { } journal)
        {
            RollbackStop? stop = await InstallRollback.TryRunAsync(newDisk(), outside, logger, journal, failures.Subject.GameFolder, failures.Subject.InstallFolder);
            outcome = stop is not null ? FailureOutcome.RollbackIncomplete
                : apply.Changed ? FailureOutcome.RolledBack
                : FailureOutcome.NothingWritten;
        }

        return failures.Failed(FailureOperation.Install, failure, outcome);
    }
}
