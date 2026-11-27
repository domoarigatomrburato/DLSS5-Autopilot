using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// Layers released by an update that no longer registers them, layer files no registration needs deleted (by the cleanup
/// at start when a program held them), and registry values that cannot be deleted given back to the caller.
/// </summary>
[TestClass]
public sealed class LayerReleaseTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task An_update_that_drops_the_vr_box_releases_the_openxr_layer_at_commit_and_keeps_the_shared_dll()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.OpenXr));

        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.IsNull(bed.Registered(RegisteredLayer.OpenXr));
        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer64));
        Assert.AreEqual("ReShade64.dll|ReShade64.json", bed.LayerFiles());
        Assert.AreEqual(bed.Ours(RegisteredLayer.VulkanLayer64), await bed.RegistrationsAsync(Ct));
        Assert.IsNull(await bed.ReleasingAsync(Ct));
    }

    [TestMethod]
    public async Task An_update_that_drops_the_vr_box_keeps_the_openxr_layer_another_game_uses()
    {
        using TransactionBed first = New();
        using TransactionBed second = first.Beside(GameFolders.Empty);
        _ = await first.CommitAsync(Ct, Layer(RegisteredLayer.OpenXr));
        _ = await second.CommitAsync(Ct, Layer(RegisteredLayer.OpenXr));

        _ = await first.CommitAsync(Ct, Dxgi());

        Assert.AreEqual(0, first.Registered(RegisteredLayer.OpenXr));
        Assert.AreEqual("ReShade64.dll|ReShade64_XR.json", first.LayerFiles());
    }

    [TestMethod]
    public async Task A_layer_dll_a_program_holds_at_uninstall_is_deleted_by_the_cleanup_at_start()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        bed.Seam = new HeldPaths().Hold(bed.LayerFile("ReShade64.dll")).Seam;
        _ = await bed.UninstallFinishedAsync(Ct);
        string left = bed.LayerFiles();
        bed.Seam = DiskSeam.Real;
        await bed.Transaction().CleanUpLayerFilesAsync();

        Assert.IsNull(bed.Registered(RegisteredLayer.VulkanLayer64));
        Assert.AreEqual("ReShade64.dll", left);
        Assert.AreEqual("", bed.LayerFiles());
    }

    [TestMethod]
    public async Task The_cleanup_at_start_leaves_the_files_of_a_registered_layer()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));
        TestFiles.Plant(bed.LayerFile("ReShade64.json.ic-1234-0123abcd.tmp"), "half of it");

        await bed.Transaction().CleanUpLayerFilesAsync();

        Assert.AreEqual("ReShade64.dll|ReShade64.json", bed.LayerFiles());
    }

    [TestMethod]
    public async Task The_cleanup_at_start_deletes_nothing_while_the_registry_cannot_be_read()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));
        _ = bed.User().RefuseReads(VulkanKey);

        await bed.Transaction().CleanUpLayerFilesAsync();

        Assert.AreEqual("ReShade64.dll|ReShade64.json", bed.LayerFiles());
    }

    [TestMethod]
    public async Task A_registry_value_that_cannot_be_deleted_leaves_the_record_removing_with_it()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));
        _ = bed.User().RefuseWrites(VulkanKey);

        bool kept = (await bed.UninstallFinishedAsync(Ct)) is { OutsideKept: true, Complete: false };

        Assert.IsTrue(kept, "the uninstall says what it left");
        Assert.IsTrue(await IsRemovingWithTheLayerAsync(bed, Ct));
        Assert.AreEqual("ReShade64.dll|ReShade64.json", bed.LayerFiles());
    }

    private static async Task<bool> IsRemovingWithTheLayerAsync(TransactionBed bed, CancellationToken cancellationToken) =>
        await bed.RecordAsync(cancellationToken) is { State: RecordState.Removing, Registrations: [{ Layer: RegisteredLayer.VulkanLayer64 }] };

    [TestMethod]
    public async Task An_uninstall_of_the_last_game_removes_the_layers_values_and_files()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.OpenXr));

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished.Complete);
        Assert.AreEqual("", bed.User().Snapshot());
        Assert.AreEqual("", bed.LayerFiles());
    }
}
