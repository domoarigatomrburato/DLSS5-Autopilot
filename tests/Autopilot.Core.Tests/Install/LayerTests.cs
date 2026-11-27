using Autopilot.Core.Install;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// The Vulkan and OpenXR layers an install registers for the current user: ours written to the layer folder, its manifest
/// pointing at the DLL beside it, and registered.
/// </summary>
[TestClass]
public sealed class LayerTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_vulkan_game_gets_our_layer_registered_for_the_user()
    {
        using TransactionBed bed = New();

        InstallCommitted committed = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer64));
        Assert.AreEqual("ReShade64.dll|ReShade64.json", bed.LayerFiles());
        Assert.IsTrue(bed.LayerFileHolds("ReShade64.dll", Dll64));
        Assert.AreEqual(bed.Ours(RegisteredLayer.VulkanLayer64), await bed.RegistrationsAsync(Ct));
        Assert.IsNotNull(committed.Said("EVERY Vulkan application", "administrator"));
    }

    [TestMethod]
    public async Task The_manifest_points_at_the_dll_beside_it_and_keeps_the_rest()
    {
        using TransactionBed bed = New();

        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(@".\ReShade64.dll", bed.ManifestValue(RegisteredLayer.VulkanLayer64, "layer", "library_path"));
        Assert.AreEqual("VK_LAYER_reshade", bed.ManifestValue(RegisteredLayer.VulkanLayer64, "layer", "name"));
        Assert.AreEqual("1", bed.ManifestValue(RegisteredLayer.VulkanLayer64, "layer", "disable_environment", "DISABLE_RESHADE"));
        Assert.AreEqual("1.0.0", bed.ManifestValue(RegisteredLayer.VulkanLayer64, "file_format_version"));
    }

    [TestMethod]
    public async Task A_32_bit_game_gets_the_32_bit_layer_under_its_own_name()
    {
        using TransactionBed bed = New();

        _ = await bed.Commit32Async(Ct, Layer(RegisteredLayer.VulkanLayer32));

        Assert.AreEqual("VK_LAYER_reshade32", bed.ManifestValue(RegisteredLayer.VulkanLayer32, "layer", "name"));
        Assert.AreEqual(@".\ReShade32.dll", bed.ManifestValue(RegisteredLayer.VulkanLayer32, "layer", "library_path"));
        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer32));
        Assert.AreEqual("ReShade32.dll|ReShade32.json", bed.LayerFiles());
    }

    [TestMethod]
    public async Task A_64_bit_vr_game_gets_the_openxr_layer_beside_the_shared_dll()
    {
        using TransactionBed bed = New();

        InstallCommitted committed = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.OpenXr));

        Assert.AreEqual(0, bed.Registered(RegisteredLayer.OpenXr));
        Assert.AreEqual(@".\ReShade64.dll", bed.ManifestValue(RegisteredLayer.OpenXr, "api_layer", "library_path"));
        Assert.AreEqual("ReShade64.dll|ReShade64.json|ReShade64_XR.json", bed.LayerFiles());
        Assert.IsNotNull(committed.Said("has not been tried with a headset"));
    }

    [TestMethod]
    public async Task The_openxr_layer_is_skipped_for_a_32_bit_game_and_the_install_goes_on()
    {
        using TransactionBed bed = New();

        InstallCommitted committed = await bed.Commit32Async(Ct, Layer(RegisteredLayer.OpenXr), Dxgi());

        Assert.IsNull(bed.Registered(RegisteredLayer.OpenXr));
        Assert.AreEqual("", bed.LayerFiles());
        Assert.AreEqual("", await bed.RegistrationsAsync(Ct));
        Assert.Contains("VR: the OpenXR layer is 64-bit only - nothing is registered for this 32-bit game.", committed.Warnings);
        Assert.IsTrue(bed.Says("VR: the OpenXR layer is 64-bit only - skipped"));
    }

    [TestMethod]
    public async Task One_layer_named_twice_is_a_plan_conflict()
    {
        using TransactionBed bed = New();

        InstallRefusal refusal = await bed.RefusalAsync(Ct, Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(InstallRefusal.PlanConflict, refusal);
    }

    [TestMethod]
    public async Task A_layer_folder_file_in_the_game_folder_is_still_released_by_a_plan_that_registers_the_layer()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Plans.Place("ReShade64.json", Dll32));

        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.IsFalse(bed.Exists("ReShade64.json"), "the step names no path of the game folder, so the previous entry is not kept");
    }

    [TestMethod]
    public void The_layer_folder_is_ours_in_local_app_data()
    {
        string local = Path.Join(Path.GetTempPath(), "Local");

        Assert.AreEqual(Path.Join(local, "invidia-claritatis", "reshade-vulkan"), SharedPlaces.LayerFolderOf(new(null, local, null, null, null)));
        Assert.IsNull(SharedPlaces.LayerFolderOf(new(null, null, null, null, null)));
    }

    [TestMethod]
    public void A_setup_manifest_that_is_no_manifest_is_refused() =>
        _ = Assert.ThrowsExactly<InvalidDataException>(() => LayerKind.Of(RegisteredLayer.OpenXr).Rewrite(SetupManifest(RegisteredLayer.VulkanLayer64)));
}
