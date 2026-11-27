using Autopilot.Core.Install;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// What an update could not release yet: tried again by the next transaction without calling it a recovery, and undone by
/// the uninstall with the rest, or kept on the record.
/// </summary>
[TestClass]
public sealed class UnreleasedTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task An_uninstall_while_the_registry_still_refuses_keeps_the_record_with_the_unreleased_layer()
    {
        using TransactionBed bed = await UpdatedWithTheOpenXrLayerUnreleasedAsync(Ct);

        _ = await bed.UninstallFinishedAsync(Ct);

        InstallRecord record = await bed.RecordAsync(Ct);
        Assert.AreEqual(RecordState.Removing, record.State);
        Assert.IsTrue(record.Releasing is { Registrations: [{ Layer: RegisteredLayer.OpenXr }] }, "the record still lists the layer it could not release");
        Assert.AreEqual(0, bed.Registered(RegisteredLayer.OpenXr));
    }

    [TestMethod]
    public async Task An_uninstall_once_the_registry_accepts_releases_the_layer_and_drops_the_record()
    {
        using TransactionBed bed = await UpdatedWithTheOpenXrLayerUnreleasedAsync(Ct);
        bed.User().Allow();

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished.Complete);
        Assert.IsNull(bed.Registered(RegisteredLayer.OpenXr));
        Assert.IsNull(await bed.FindAsync(Ct));
    }

    [TestMethod]
    public async Task A_release_tried_again_is_no_recovery()
    {
        using TransactionBed bed = await UpdatedWithTheOpenXrLayerUnreleasedAsync(Ct);

        InstallCommitted first = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));
        InstallCommitted second = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.IsFalse(first.Recovered);
        Assert.IsFalse(second.Recovered);
        Assert.IsFalse(bed.Says("left open by an earlier run"));
        Assert.AreEqual(0, bed.Registered(RegisteredLayer.OpenXr), "Windows still refuses");
    }

    // Both layers installed, then an update without the OpenXR layer whose release Windows refused.
    private static async Task<TransactionBed> UpdatedWithTheOpenXrLayerUnreleasedAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = New();
        _ = await bed.CommitAsync(cancellationToken, Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.OpenXr));
        _ = bed.User().RefuseWrites(OpenXrKey);
        _ = await bed.CommitAsync(cancellationToken, Layer(RegisteredLayer.VulkanLayer64));
        Assert.IsTrue((await bed.RecordAsync(cancellationToken)).Releasing is { Registrations: [{ Layer: RegisteredLayer.OpenXr }] }, "the update could not release the layer");
        return bed;
    }
}
