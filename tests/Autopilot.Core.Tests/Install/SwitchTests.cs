using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Switching routes: the previous route's files held until commit, deleted then, renamed back on a rollback.</summary>
[TestClass]
public sealed class SwitchTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_route_switch_holds_the_previous_files_until_commit_then_deletes_them()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallPlan y = Switches.Y(bed);
        int commit = await bed.CommitStepAsync(y, Ct);
        string heldBeforeCommit = "";
        bed.Steps = new StepHook((step, _) => step == commit, () => heldBeforeCommit = bed.HeldFiles());

        InstallCommitted committed = await bed.CommitAsync(y, Ct);

        Assert.AreEqual("dlss5-feed.addon64.invidia-claritatis-held|dxgi.dll.invidia-claritatis-held|nvngx_dlss.dll.invidia-claritatis-backup.invidia-claritatis-held", heldBeforeCommit);
        AssertSwitched(bed, committed, await bed.EntriesAsync(Ct));
    }

    [TestMethod]
    public async Task A_failed_route_switch_renames_the_held_files_back_and_returns_to_the_previous_route()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallRecord x = await bed.RecordAsync(Ct);
        TreeSnapshot before = bed.Snapshot();

        InstallFailed failed = await bed.FailAtCommitAsync(Switches.Y(bed), Ct);

        Switches.AssertBackToX(before, bed, x, await bed.RecordAsync(Ct));
        Assert.AreEqual(FailureOutcome.RolledBack, failed.Note.Outcome);
        Assert.AreEqual(RouteId.OptiScaler, failed.Note.Route);
    }

    private static void AssertSwitched(TransactionBed bed, InstallCommitted committed, string entries)
    {
        Assert.AreEqual("", bed.HeldFiles());
        Assert.IsFalse(bed.Exists("dlss5-feed.addon64"));
        Assert.IsTrue(bed.Holds("d3dcompiler_47.dll", Switches.GameCompiler), "Y does not move the compiler aside: the game's comes back");
        Assert.IsTrue(bed.Holds("dxgi.dll", Switches.YDxgi) && bed.BackupIsTheOriginal());
        Assert.AreEqual(
            "dxgi.dll Written|renodx-dlss5.addon64 Written|nvngx_dlss.dll Written|version.dll Written|reshade-shaders/Shaders/y.fx Written",
            entries);
        Assert.Contains(note => note.Contains("version.dll.invidia-claritatis-superseded", StringComparison.Ordinal), committed.Notes);
    }
}
