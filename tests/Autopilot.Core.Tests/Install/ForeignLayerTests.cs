using Autopilot.Core.Install;
using Autopilot.Core.Platform;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// A ReShade layer another install registered: reused as it is when it is active, this game's architecture can load it and
/// its manifest is there; else ours is registered beside it, and the foreign value is never touched.
/// </summary>
[TestClass]
public sealed class ForeignLayerTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task An_active_foreign_layer_for_this_architecture_is_reused_and_nothing_is_written(bool machine)
    {
        using TransactionBed bed = New();
        string foreign = PlantForeign(bed, "ReShade64.json", "ReShade64.dll");
        Register(bed, machine, foreign, RegistryView.Registry64);
        string registry = bed.User().Snapshot();

        InstallCommitted committed = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(registry, bed.User().Snapshot());
        Assert.AreEqual("", bed.LayerFiles());
        Assert.AreEqual(SharedReads.Reused(RegisteredLayer.VulkanLayer64, foreign), await bed.RegistrationsAsync(Ct));
        Assert.IsNotNull(committed.Said(foreign));
    }

    [TestMethod]
    public async Task A_disabled_foreign_layer_is_not_reused()
    {
        using TransactionBed bed = New();
        string foreign = PlantForeign(bed, "ReShade64.json", "ReShade64.dll");
        _ = bed.User().Set(VulkanKey, foreign, 1);

        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer64));
        Assert.AreEqual(1, bed.User().Value(VulkanKey, foreign), "the foreign value is left as it was");
    }

    [TestMethod]
    public async Task A_foreign_layer_for_the_other_architecture_is_not_reused()
    {
        using TransactionBed bed = New();
        _ = bed.User().Set(VulkanKey, PlantForeign(bed, "ReShade.json", "ReShade32.dll"), 0);

        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer64));
    }

    [TestMethod]
    public async Task The_machines_32_bit_view_is_never_consulted()
    {
        using TransactionBed bed = New();
        Register(bed, machine: true, PlantForeign(bed, "ReShade32.json", "ReShade32.dll"), RegistryView.Registry32);

        _ = await bed.Commit32Async(Ct, Layer(RegisteredLayer.VulkanLayer32));

        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer32));
    }

    [TestMethod]
    public async Task A_foreign_value_whose_manifest_is_gone_is_not_reused()
    {
        using TransactionBed bed = New();
        _ = bed.User().Set(VulkanKey, Path.Join(bed.Tree.Root, "elsewhere", "ReShade64.json"), 0);

        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer64));
    }

    private static string PlantForeign(TransactionBed bed, string manifest, string library)
    {
        string path = Path.GetFullPath(Path.Join(bed.Tree.Root, "..", "ReShade", manifest));
        TestFiles.Plant(path, $$"""{ "file_format_version": "1.0.0", "layer": { "name": "VK_LAYER_reshade", "library_path": ".\\{{library}}" }, "api_layer": { "library_path": "{{library}}" } }""");
        return path;
    }

    // The foreign layer's value, enabled, under the current user's key or the machine's in the given view.
    private static void Register(TransactionBed bed, bool machine, string manifest, RegistryView view)
    {
        if (machine)
        {
            _ = bed.Machine().Set(RegistryRoot.LocalMachine, view, VulkanKey, manifest, 0);
        }
        else
        {
            _ = bed.User().Set(VulkanKey, manifest, 0);
        }
    }
}
