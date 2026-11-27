using Autopilot.Core.State;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>The disks a transaction's runs make their changes through, and the undo of the changes outside the files over them.</summary>
/// <param name="records">Saves the record.</param>
/// <param name="steps">Told before each change.</param>
/// <param name="time">The clock the retries wait on.</param>
/// <param name="seam">The rename, replace and delete the changes go through.</param>
/// <param name="store">Names every folder the tool installed into: who else relies on a layer.</param>
/// <param name="logger">Where the undo is logged.</param>
/// <param name="shared">The registry and the layer folder; null when there are none, and nothing outside the files is undone.</param>
internal sealed class TransactionDisks(InstallRecords records, IWriteSteps steps, TimeProvider time, DiskSeam seam, GameStateStore store, ILogger logger, SharedPlaces? shared)
{
    /// <summary>Undoes the registrations and graphics preferences of one run, through its disk.</summary>
    public IOutsideWork Outside { get; } = OutsideWork.Of(shared, store, records, logger);

    /// <summary>A disk whose held files are tried with the atomic write's waits.</summary>
    public InstallDisk New() => new(records, steps, time, seam);

    /// <summary>A disk for a removal part way, a held file tried a little longer, as the uninstall does.</summary>
    public InstallDisk Removal() => RecordRemoval.NewDisk(records, steps, time, seam);

    /// <summary>
    /// The recovery of the transaction left open in <paramref name="installFolder"/>: a removal part way finished on the removal
    /// disk when <paramref name="removal"/>, else on a plain one, and only while <paramref name="gameRuns"/> says no.
    /// </summary>
    public TransactionRecovery Recovery(string installFolder, Failures failures, bool removal, Func<bool> gameRuns)
    {
        InstallDisk removalDisk = removal ? Removal() : New();
        return new(New, installFolder, failures, Outside, new RecordRemoval(removalDisk, Outside.For(removalDisk, installFolder), logger), gameRuns, logger);
    }

    /// <summary>One install's run, its plan's shared steps resolved against the registry and the layer folder.</summary>
    public InstallRun Run(Failures failures) => new(New, Outside, logger, failures, shared);

    /// <summary>Deletes the layer folder's files no registration needs any more; nothing without a layer folder.</summary>
    public Task CleanUpLayerFilesAsync() => Outside.CleanUpLayerFilesAsync(New());
}
