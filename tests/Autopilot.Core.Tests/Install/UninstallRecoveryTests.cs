using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A transaction left open finished before an uninstall, at start, or before an install, by the uninstall's own path.</summary>
[TestClass]
public sealed class UninstallRecoveryTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task An_install_left_open_over_an_earlier_one_is_rolled_back_first_then_uninstalled()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        bed.Steps = CrashPoints.After((await bed.MadeStepsAsync(Switches.Y(bed), Ct)).Count / 2);
        _ = await bed.InstallAsync(Switches.Y(bed), Ct);
        bed.Steps = NoWriteSteps.Instance;

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished is { Recovered: true, Complete: true });
        Assert.IsTrue(bed.Holds("dxgi.dll", Switches.GameDxgi) && bed.Holds("nvngx_dlss.dll", Switches.GameDlss));
        Assert.IsFalse(bed.Exists("dlss5-feed.addon64") || bed.Exists("renodx-dlss5.addon64") || bed.Exists(KnownFiles.InstallRecord));
    }

    [TestMethod]
    public async Task A_removal_left_part_way_is_not_finished_at_start_while_the_game_runs()
    {
        using TransactionBed bed = await RemovalLeftAsync(Ct);
        _ = bed.Processes.Run(bed.Tree.PathOf("Game.exe"));

        InstallOutcome? recovered = await bed.RecoverAsync(Ct);

        Assert.IsNull(recovered);
        Assert.AreEqual(RecordState.Removing, (await bed.RecordAsync(Ct)).State);
        Assert.IsTrue(bed.Exists("a.dll"));
    }

    [TestMethod]
    public async Task A_removal_finished_at_start_keeps_the_games_state_and_the_failure_note_in_it()
    {
        using TransactionBed bed = await RemovalLeftAsync(Ct);
        bed.SetAttempts();

        InstallOutcome? recovered = await bed.RecoverAsync(Ct);

        Assert.IsNull(recovered);
        Assert.IsTrue(await bed.FindAsync(Ct) is null && !bed.Exists("a.dll"));
        Assert.AreEqual(3, bed.Attempts());
    }

    [TestMethod]
    public async Task An_install_over_a_removal_left_part_way_finishes_it_first()
    {
        using TransactionBed bed = await RemovalLeftAsync(Ct);

        InstallCommitted committed = await bed.CommitAsync(bed.OursPlan("b.dll"), Ct);

        Assert.IsTrue(committed.Recovered && !bed.Exists("a.dll"));
        Assert.AreEqual("b.dll Written", await bed.EntriesAsync(Ct));
    }

    [TestMethod]
    public async Task An_install_over_a_removal_that_cannot_be_finished_is_refused()
    {
        using TransactionBed bed = await RemovalLeftAsync(Ct);
        bed.Seam = new HeldPaths().Hold(bed.Tree.PathOf("a.dll")).Seam;

        InstallRefused refused = await bed.InstallAsAsync<InstallRefused>(bed.OursPlan("b.dll"), Ct);

        Assert.AreEqual(InstallRefusal.RemovalUnfinished, refused.Reason);
        Assert.IsFalse(bed.Exists("b.dll"));
    }

    // Our a.dll, with an uninstall that crashed once it had saved its record as removing.
    private static async Task<TransactionBed> RemovalLeftAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = await UninstallBed.OursAsync(cancellationToken, "a.dll");
        bed.Steps = CrashPoints.After(1);
        _ = await bed.UninstallAsync(cancellationToken);
        bed.Steps = NoWriteSteps.Instance;
        return bed;
    }
}
