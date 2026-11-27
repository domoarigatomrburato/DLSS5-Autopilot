using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A failed fresh install leaves the game folder byte for byte as it was, temporary files included.</summary>
[TestClass]
public sealed class RollbackTests(TestContext testContext)
{
    private static readonly byte[] _addon = GameFolders.Dll("our add-on");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_fresh_install_rolled_back_leaves_the_folder_byte_for_byte_as_before()
    {
        using TransactionBed bed = new(GameFolders.OwnFiles);
        TreeSnapshot before = bed.Snapshot();

        // The drive fills as the install commits: everything it wrote is on disk by then.
        InstallFailed failed = await bed.FailAtCommitAsync(bed.Plan(RouteId.Native, Plans.Place("dlss5-feed.addon64", _addon), Plans.Place("reshade-shaders/Shaders/a.fx", _addon)), Ct);

        before.AssertSame(bed.Snapshot());
        AssertRolledBack(bed, failed);
    }

    [TestMethod]
    public async Task A_rollback_removes_the_temporary_files_a_crashed_write_left()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        TreeSnapshot before = bed.Snapshot();
        await CrashMidWriteAsync(bed, Ct);

        Assert.IsNull(await bed.RecoverAsync(Ct));

        before.AssertSame(bed.Snapshot());
    }

    /// <summary>An install crashing as it writes, its temporary file left behind.</summary>
    private static async Task CrashMidWriteAsync(TransactionBed bed, CancellationToken cancellationToken)
    {
        bed.Steps = CrashPoints.After(3);
        _ = await bed.InstallAsync(bed.Plan(RouteId.Native, Plans.Place("Shaders/a.fx", _addon)), cancellationToken);
        TestFiles.Plant(bed.Tree.PathOf("Shaders/a.fx.ic-1234-0123abcd.tmp"), "half of it"u8.ToArray());
    }

    [TestMethod]
    public async Task A_temporary_file_gone_before_the_rollback_deletes_it_does_not_stop_the_rollback()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        await CrashMidWriteAsync(bed, Ct);
        VanishAsDeleted(bed, bed.Tree.PathOf("Shaders/a.fx.ic-1234-0123abcd.tmp"));

        InstallCommitted next = await bed.CommitAsync(bed.Plan(RouteId.Native, new CreateFolder("Other")), Ct);

        AssertWentOn(bed, next);
    }

    // The read-only temporary file goes as its attribute is about to be cleared.
    private static void VanishAsDeleted(TransactionBed bed, string temporary)
    {
        File.SetAttributes(temporary, FileAttributes.ReadOnly);
        bed.Steps = new StepHook((_, step) => step.Path == temporary, () => File.Delete(temporary));
    }

    private static void AssertWentOn(TransactionBed bed, InstallCommitted next)
    {
        Assert.IsTrue(next.Recovered);
        Assert.IsFalse(Directory.Exists(bed.Tree.PathOf("Shaders")), "the rollback went on to the folders");
    }

    private static void AssertRolledBack(TransactionBed bed, InstallFailed failed)
    {
        Assert.IsFalse(Directory.Exists(bed.Tree.PathOf("reshade-shaders")), "the folders it created go too");
        Assert.AreEqual(FailureOutcome.RolledBack, failed.Note.Outcome);
        Assert.AreEqual(failed.Note.Text, bed.Note?.Text);
    }
}
