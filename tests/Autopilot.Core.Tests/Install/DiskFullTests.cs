using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A full drive at any step before the commit: the folder as it was, and a note naming the drive.</summary>
[TestClass]
public sealed class DiskFullTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    [DataRow(nameof(DiskFullError.Windows112), false)]
    [DataRow(nameof(DiskFullError.Windows39), false)]
    [DataRow(nameof(DiskFullError.Errno28), false)]
    [DataRow(nameof(DiskFullError.Windows112), true)]
    public async Task A_full_drive_at_any_step_leaves_the_folder_as_it_was_and_names_the_drive(string error, bool wrapped)
    {
        int steps = await StepsAsync(Ct);
        for (int at = 1; at <= steps; at++)
        {
            await FillAtAsync(at, Enum.Parse<DiskFullError>(error), wrapped, Ct);
        }
    }

    private static async Task<int> StepsAsync(CancellationToken cancellationToken)
    {
        using TransactionBed bed = new(Switches.GameFiles);
        return (await bed.MadeStepsAsync(Switches.X(bed), cancellationToken)).Count;
    }

    private static async Task FillAtAsync(int at, DiskFullError error, bool wrapped, CancellationToken cancellationToken)
    {
        using TransactionBed bed = new(Switches.GameFiles);
        TreeSnapshot before = bed.Snapshot();
        bed.Steps = DiskFullPoints.At(at, error, wrapped);

        InstallFailed failed = await bed.InstallAsAsync<InstallFailed>(Switches.X(bed), cancellationToken);

        AssertFull($"full at step {at}", before, bed, failed);
    }

    private static void AssertFull(string at, TreeSnapshot before, TransactionBed bed, InstallFailed failed)
    {
        Assert.IsNull(before.Difference(bed.Snapshot()), at);
        Assert.AreEqual(FailureKind.DiskFull, failed.Note.Kind, at);
        // A backup rename the full drive stops says so in the backup sentence; every other step names the drive.
        string text = failed.Note.Text;
        Assert.IsTrue(text.Contains("G:", StringComparison.Ordinal) || text.Contains(" could not be backed up (", StringComparison.Ordinal), $"{at}: {text}");
        Assert.AreNotEqual(FailureOutcome.RollbackIncomplete, failed.Note.Outcome, at);
    }
}
