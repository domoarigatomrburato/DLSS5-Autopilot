using Autopilot.Core.Platform;
using Autopilot.Core.State;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Runs an install plan as one transaction: the record saved before the first change and before each group,
/// every change named on it first; the previous install's files held until commit; a check that what was
/// written is still there; and on any failure, cancellation included, a rollback to the folder as it was,
/// with the reason kept as the folder's failure note. A transaction an earlier run left open is finished first.
/// </summary>
internal sealed class InstallTransaction
{
    private readonly InstallRecords _records;
    private readonly GameStateStore _store;
    private readonly IFreeSpace _space;
    private readonly IProcesses _processes;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly IWriteSteps _steps;
    private readonly DiskSeam _seam;
    private readonly IOutsideChanges _outside = OutsideChanges.NotUndoneYet;

    /// <param name="records">Reads and saves the record.</param>
    /// <param name="store">Keeps the failure note.</param>
    /// <param name="space">Names the drives of a full-drive failure.</param>
    /// <param name="processes">The running programs: a removal left part way is not finished while the game runs.</param>
    /// <param name="time">The clock of the note and of the retries.</param>
    /// <param name="logger">Where the transaction's lines go.</param>
    public InstallTransaction(InstallRecords records, GameStateStore store, IFreeSpace space, IProcesses processes, TimeProvider time, ILogger<InstallTransaction> logger)
        : this(records, store, space, processes, time, logger, NoWriteSteps.Instance, DiskSeam.Real)
    {
    }

    /// <summary>The transaction with every change told to <paramref name="steps"/> first and made through <paramref name="seam"/>: the tests' crash, full-drive and lock points.</summary>
    internal InstallTransaction(InstallRecords records, GameStateStore store, IFreeSpace space, IProcesses processes, TimeProvider time, ILogger logger, IWriteSteps steps, DiskSeam seam)
    {
        _records = records;
        _store = store;
        _space = space;
        _processes = processes;
        _time = time;
        _logger = logger;
        _steps = steps;
        _seam = seam;
    }

    /// <summary>
    /// Installs <paramref name="plan"/>. Refused, writing nothing, when the folder's record cannot be read or
    /// belongs elsewhere, or the plan reaches outside the game folder or names a path twice. Otherwise the
    /// folder's failure note is cleared, an open transaction an earlier run left is finished, and the plan runs;
    /// a removal left part way that cannot be finished now refuses it.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled: the install was rolled back and its note says so.</exception>
    /// <exception cref="ArgumentException">A step of the plan is still a pattern: only the preview reads such a plan.</exception>
    public async Task<InstallOutcome> RunAsync(InstallPlan plan, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.HasPatterns)
        {
            throw new ArgumentException("a plan with patterns is the preview's: gather the real file lists first", nameof(plan));
        }

        string installFolder = Path.GetFullPath(plan.InstallFolder);
        RecordedInstall? recorded = await _records.FindAsync(installFolder, cancellationToken);
        return InstallChecks.RefusalOf(plan, recorded) is { } refused
            ? refused
            : await RunCheckedAsync(plan, recorded, installFolder, progress, cancellationToken);
    }

    // Past the refusals: the note cleared, an open transaction finished (and the folder's record read and
    // checked again after it), then the plan.
    private async Task<InstallOutcome> RunCheckedAsync(InstallPlan plan, RecordedInstall? recorded, string installFolder, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        Failures failures = FailuresOf(new(plan.GameFolder, installFolder, plan.Header.Route, plan.Header.ToolVersion));
        failures.ClearNote(cancellationToken);
        Recovery recovery = await Recovery(installFolder, failures, RecordRemoval.NewDisk(_records, _steps, _time, _seam)).RunAsync(recorded, CancellationToken.None);
        InstallOutcome outcome = await AfterRecoveryAsync(plan, recovery.Ran ? await _records.FindAsync(installFolder, cancellationToken) : recorded, recovery, failures, progress, cancellationToken);
        return outcome with { Recovered = recovery.Ran };
    }

    // The failure when the open transaction could not be finished; else the plan, over the record as it now is.
    private async Task<InstallOutcome> AfterRecoveryAsync(InstallPlan plan, RecordedInstall? recorded, Recovery recovery, Failures failures, IProgress<InstallProgress>? progress, CancellationToken cancellationToken) =>
        recovery.Blocking
            ?? (recovery.Ran ? InstallChecks.RefusalOf(plan, recorded) : null)
            ?? await new InstallRun(NewDisk, _outside, _logger, failures).RunAsync(plan, recorded, progress, cancellationToken);

    /// <summary>
    /// Finishes, and does nothing else, a transaction an earlier run left open in <paramref name="installFolder"/>
    /// (as the tool does at start for the folders it knows). A removal part way is finished as an uninstall
    /// would, a held file tried only for a moment; while the game runs, or with something left, its record stays
    /// for the next uninstall, and the per-game state, the failure note with it, stays too.
    /// Null when the folder holds none, it was finished, or it was a removal;
    /// a refusal when its record cannot be read or lies outside <paramref name="gameFolder"/>; the failure when
    /// it cannot be finished, kept as the folder's note.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled before the record was read.</exception>
    public async Task<InstallOutcome?> RecoverAsync(string gameFolder, string installFolder, string toolVersion, CancellationToken cancellationToken)
    {
        installFolder = Path.GetFullPath(installFolder);
        RecordedInstall? recorded = await _records.FindAsync(installFolder, cancellationToken);
        return InstallChecks.RecordRefusal(new(gameFolder), recorded) is { } refused
            ? refused
            : (await Recovery(installFolder, FailuresOf(new(gameFolder, installFolder, recorded?.Record?.Route, toolVersion)), NewDisk()).RunAsync(recorded, CancellationToken.None)).Failed is { } failed
                ? failed with { Recovered = true }
                : null;
    }

    private Failures FailuresOf(FailureSubject subject) => new(_store, _space, _time, _logger, subject);

    // A removal part way is finished on removalDisk, and only while no program from the game or install folder runs.
    private TransactionRecovery Recovery(string installFolder, Failures failures, InstallDisk removalDisk) =>
        new(
            NewDisk,
            installFolder,
            failures,
            _outside,
            new RecordRemoval(removalDisk, _outside, _logger),
            () => Uninstaller.IsGameRunning(_processes, failures.Subject.GameFolder, installFolder),
            _logger);

    private InstallDisk NewDisk() => new(_records, _steps, _time, _seam);
}
