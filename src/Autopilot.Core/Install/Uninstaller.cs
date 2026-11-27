using System.Runtime.ExceptionServices;
using Autopilot.Core.Platform;
using Autopilot.Core.State;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Uninstalls a game: from its install record when there is one (what the record names removed, what it replaced
/// put back, and nothing else), or by known file names when the record is missing, damaged or the wrong shape.
/// Refused while the game runs. Idempotent and resumable: what could not be removed stays on the record, and
/// the next uninstall finishes the job.
/// </summary>
public sealed class Uninstaller
{
    private readonly InstallRecords _records;
    private readonly GameStateStore _store;
    private readonly IFreeSpace _space;
    private readonly IProcesses _processes;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly IWriteSteps _steps;
    private readonly DiskSeam _seam;
    private readonly IOutsideChanges _outside;

    /// <param name="records">Reads and saves the record.</param>
    /// <param name="store">The per-game state: the failure note, dropped with the rest once nothing is left.</param>
    /// <param name="space">Names the drives of a full-drive failure.</param>
    /// <param name="processes">The running programs, to refuse while the game runs.</param>
    /// <param name="time">The clock of the note and of the retries.</param>
    /// <param name="logger">Where the uninstall's lines go.</param>
    public Uninstaller(InstallRecords records, GameStateStore store, IFreeSpace space, IProcesses processes, TimeProvider time, ILogger<Uninstaller> logger)
        : this(records, store, space, processes, time, logger, NoWriteSteps.Instance, DiskSeam.Real, OutsideChanges.NotUndoneYet)
    {
    }

    /// <summary>The uninstall with every change told to <paramref name="steps"/> first and made through <paramref name="seam"/>, and the changes outside the files undone by <paramref name="outside"/>.</summary>
    internal Uninstaller(InstallRecords records, GameStateStore store, IFreeSpace space, IProcesses processes, TimeProvider time, ILogger logger, IWriteSteps steps, DiskSeam seam, IOutsideChanges outside)
    {
        _records = records;
        _store = store;
        _space = space;
        _processes = processes;
        _time = time;
        _logger = logger;
        _steps = steps;
        _seam = seam;
        _outside = outside;
    }

    /// <summary>
    /// Uninstalls what was installed in <paramref name="installFolder"/>. Refused, changing nothing, while a program
    /// from the install or game folder runs, when the record is newer than this build or cannot be read right now,
    /// and when it names a game folder outside <paramref name="gameFolder"/>. A transaction an earlier run left open
    /// is finished first. The folder's failure note is cleared as it starts.
    /// </summary>
    /// <param name="gameFolder">The game folder: nothing outside it is touched.</param>
    /// <param name="installFolder">The folder holding the executable and the record.</param>
    /// <param name="toolVersion">The build running, for the failure note.</param>
    /// <param name="cancellationToken">Stops between two paths of the record; what was not reached stays on it.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled: the record lists what is left.</exception>
    public async Task<UninstallOutcome> RunAsync(string gameFolder, string installFolder, string toolVersion, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(installFolder);
        installFolder = Path.GetFullPath(installFolder);
        RecordedInstall? recorded = await _records.FindAsync(installFolder, cancellationToken);
        return RefusalOf(gameFolder, installFolder, recorded) is { } refused
            ? refused
            : await new Run(this, gameFolder, installFolder, toolVersion, recorded).RunAsync(cancellationToken);
    }

    /// <summary>Whether a program whose image lies inside <paramref name="installFolder"/> or <paramref name="gameFolder"/> is running.</summary>
    internal static bool IsGameRunning(IProcesses processes, string gameFolder, string installFolder) =>
        RunningGames.Inside(processes.List(), [Path.GetFullPath(installFolder), Path.GetFullPath(gameFolder)]).Count > 0;

    // A newer record, or one unreadable now, is never guessed at; nor is a record naming another game folder; and
    // nothing is touched while the game runs. A damaged record is no refusal: it falls back to known names.
    private UninstallRefused? RefusalOf(string gameFolder, string installFolder, RecordedInstall? recorded)
    {
        if (recorded is { Record: null, Problem: RecordProblem.Newer or RecordProblem.Inaccessible })
        {
            return new(UninstallRefusal.RecordUnreadable) { Problem = recorded.Problem };
        }

        if (recorded?.Record is not null && InstallChecks.RecordRefusal(new(gameFolder), recorded) is not null)
        {
            return new(UninstallRefusal.RecordOutsideGameFolder);
        }

        return IsGameRunning(_processes, gameFolder, installFolder) ? new(UninstallRefusal.GameRunning) : null;
    }

    /// <summary>One uninstall of one folder, past its refusals, on one disk.</summary>
    private sealed class Run(Uninstaller owner, string gameFolder, string installFolder, string toolVersion, RecordedInstall? recorded)
    {
        private readonly InstallDisk _disk = RecordRemoval.NewDisk(owner._records, owner._steps, owner._time, owner._seam);

        // The note cleared first; an error that stops the uninstall is kept as the note; nothing left drops the per-game state.
        public async Task<UninstallOutcome> RunAsync(CancellationToken cancellationToken)
        {
            Failures failures = FailuresOf();
            failures.ClearNote(cancellationToken);
            try
            {
                UninstallOutcome outcome = recorded?.Record is null
                    ? await new KnownNamesCleanup(_disk, owner._logger).RunAsync(recorded?.Problem, gameFolder, installFolder, cancellationToken)
                    : await FromRecordAsync(failures, cancellationToken);
                if (outcome is UninstallFinished { Complete: true })
                {
                    // Nothing of the install is left: its verdicts, measurements, plans and note go with it.
                    owner._store.Drop(GameIdentity.Of(installFolder), CancellationToken.None);
                }

                return outcome;
            }
            catch (Exception stopped) when (stopped is not OperationCanceledException)
            {
                return new UninstallFailed(failures.UninstallStopped(stopped, _disk.Changed));
            }
        }

        private Failures FailuresOf() => new(owner._store, owner._space, owner._time, owner._logger, new(gameFolder, installFolder, recorded?.Record?.Route, toolVersion));

        // An open transaction goes first: a removal part way is this uninstall's own; after a rollback or a commit, the record as it then is goes.
        private async Task<UninstallOutcome> FromRecordAsync(Failures failures, CancellationToken cancellationToken)
        {
            RecordRemoval removal = new(_disk, owner._outside, owner._logger);
            Recovery recovery = await RecoverAsync(failures, removal, cancellationToken);
            if (recovery.Failed is { } failed)
            {
                return new UninstallFailed(failed.Note) { Recovered = true };
            }

            if (recovery.RemovalStopped is { } stopped)
            {
                ExceptionDispatchInfo.Throw(stopped);
            }

            if (recovery.Removal is { } removed)
            {
                return removed;
            }

            // An open install rolled back whole leaves nothing to remove.
            RecordedInstall? found = recovery.Ran ? await owner._records.FindAsync(installFolder, cancellationToken) : recorded;
            UninstallFinished finished = found is { Record: { } record, GameFolder: { } recordGameFolder }
                ? await removal.RunAsync(record, recordGameFolder, installFolder, cancellationToken)
                : new(0, [], 0);
            return finished with { Recovered = recovery.Ran };
        }

        // The game was found not running as the uninstall started.
        private Task<Recovery> RecoverAsync(Failures failures, RecordRemoval removal, CancellationToken cancellationToken) =>
            new TransactionRecovery(() => _disk, installFolder, failures, owner._outside, removal, static () => false, owner._logger).RunAsync(recorded, cancellationToken);
    }
}
