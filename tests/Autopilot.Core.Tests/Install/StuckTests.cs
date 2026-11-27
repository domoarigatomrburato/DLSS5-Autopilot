using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A rollback another program stopped: the next install finishes it first, as an uninstall would, and is refused while the file stays held.</summary>
[TestClass]
public sealed class StuckTests(TestContext testContext)
{
    private static readonly byte[] _ours = GameFolders.Dll("ours");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_next_install_finishes_a_stuck_rollback_first_once_the_file_is_let_go()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        HeldPaths held = await StuckAsync(bed, Ct);

        InstallRefused again = await bed.InstallAsAsync<InstallRefused>(OneFile(bed), Ct);
        held.Release(bed.Tree.PathOf("a.dll"));
        InstallCommitted next = await bed.CommitAsync(OneFile(bed), Ct);

        AssertFinished(again, next, bed);
    }

    // An install of a.dll whose rollback stops on a.dll, held by another program.
    private static async Task<HeldPaths> StuckAsync(TransactionBed bed, CancellationToken cancellationToken)
    {
        HeldPaths held = new();
        bed.Seam = held.Seam;
        InstallPlan plan = OneFile(bed);
        _ = await bed.LookAtCommitAsync(plan, look => held.Hold(look.Tree.PathOf("a.dll")), cancellationToken);
        _ = await bed.FailAtCommitAsync(plan, cancellationToken);
        bed.Steps = Core.Storage.NoWriteSteps.Instance;
        return held;
    }

    private static InstallPlan OneFile(TransactionBed bed) => bed.Plan(RouteId.Native, Plans.Place("a.dll", _ours));

    private static void AssertFinished(InstallRefused again, InstallCommitted next, TransactionBed bed)
    {
        Assert.AreEqual(InstallRefusal.RemovalUnfinished, again.Reason);
        Assert.IsTrue(again.Recovered);
        Assert.IsTrue(next.Recovered);
        Assert.IsTrue(bed.Holds("a.dll", _ours));
    }
}
