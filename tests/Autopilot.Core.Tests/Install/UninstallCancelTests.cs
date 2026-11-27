using Autopilot.Core.Install;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Uninstall cancelled, or stopped between two paths: what was not reached stays on the record, and the next uninstall finishes it.</summary>
[TestClass]
public sealed class UninstallCancelTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_cancelled_uninstall_finishes_the_path_it_is_on_and_leaves_the_rest_on_the_record()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll", "b.dll");
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        bed.CancelAtFirstDelete(cancel);

        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => bed.UninstallAsync(cancel.Token));

        Assert.AreEqual("a.dll Written", await bed.EntriesAsync(Ct));
        Assert.IsTrue(bed.Exists("a.dll") && !bed.Exists("b.dll"));
    }

    [TestMethod]
    public async Task The_uninstall_after_one_that_stopped_part_way_finishes_the_job()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll", "b.dll");
        bed.Steps = CrashPoints.After(2);
        _ = await bed.UninstallAsync(Ct);
        bed.Steps = NoWriteSteps.Instance;

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished.Complete && !bed.Exists("a.dll") && !bed.Exists("b.dll"));
        Assert.IsNull(await bed.FindAsync(Ct));
    }
}
