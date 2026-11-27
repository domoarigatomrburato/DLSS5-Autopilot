using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A rollback that could not put everything back: what is left keeps its kind, and the next transaction finishes it.</summary>
[TestClass]
public sealed class RollbackStuckTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_held_file_and_an_original_another_program_kept_are_put_back_by_the_next_recovery()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallRecord x = await bed.RecordAsync(Ct);
        TreeSnapshot before = bed.Snapshot();
        HeldPaths held = await HoldDuringRollbackAsync(bed, Ct);

        InstallFailed failed = await bed.FailAtCommitAsync(Switches.Y(bed), Ct);
        Release(bed, held);
        InstallOutcome? recovered = await bed.RecoverAsync(Ct);

        Assert.AreEqual(FailureOutcome.RollbackIncomplete, failed.Note.Outcome);
        Assert.IsNull(recovered);
        Switches.AssertBackToX(before, bed, x, await bed.RecordAsync(Ct));
    }

    [TestMethod]
    public async Task A_file_that_cannot_be_put_back_without_any_error_says_so_and_names_it()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallPlan y = Switches.Y(bed);
        // While held, X's add-on's place is taken: the rollback cannot rename it back.
        _ = await bed.LookAtCommitAsync(y, TakePlace, Ct);
        _ = await bed.FailAtCommitAsync(y, Ct);

        InstallFailed failed = await bed.InstallAsAsync<InstallFailed>(Switches.Y(bed), Ct);

        AssertNotPutBack(bed, failed);
    }

    // X's dxgi.dll held beside itself, and the game's compiler Y puts back, are held by another program from the commit on.
    private static async Task<HeldPaths> HoldDuringRollbackAsync(TransactionBed bed, CancellationToken cancellationToken)
    {
        HeldPaths held = new();
        bed.Seam = held.Seam;
        _ = await bed.LookAtCommitAsync(Switches.Y(bed), look => held.Hold(look.Tree.PathOf("dxgi.dll.invidia-claritatis-held")).Hold(look.Tree.PathOf("d3dcompiler_47.dll")), cancellationToken);
        return held;
    }

    private static void Release(TransactionBed bed, HeldPaths held)
    {
        held.Release(bed.Tree.PathOf("dxgi.dll.invidia-claritatis-held"));
        held.Release(bed.Tree.PathOf("d3dcompiler_47.dll"));
    }

    private static bool TakePlace(TransactionBed bed)
    {
        TestFiles.Plant(bed.Tree.PathOf("dlss5-feed.addon64"), "another program's"u8.ToArray());
        return true;
    }

    private static void AssertNotPutBack(TransactionBed bed, InstallFailed failed)
    {
        Assert.IsTrue(failed.Recovered);
        Assert.AreEqual(FailureKind.Unexpected, failed.Note.Kind);
        Assert.AreEqual(bed.Tree.PathOf("dlss5-feed.addon64"), failed.Note.Cause?.Path);
        Assert.Contains("dlss5-feed.addon64 could not be put back", failed.Note.Text);
    }
}
