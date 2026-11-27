using Autopilot.Core.Executables;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The preview's <c>outside:</c> lines for the layers and the graphics preference an install would set.</summary>
[TestClass]
public sealed class SharedPreviewTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_preview_names_each_layer_and_the_graphics_preference_outside_the_game_folder()
    {
        using TransactionBed bed = NewGame();

        IReadOnlyList<string> lines = await PreviewAsync(bed, Bitness.Bits64, Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.OpenXr), Preference());

        Assert.Contains($"outside: Vulkan layer: VK_LAYER_reshade registered for this user (files in {bed.LayerFolder()}) - it loads into EVERY Vulkan application until 'uninstall' removes it. Per user only: programs started as administrator do not get it.", lines);
        Assert.Contains($"outside: OpenXR layer (VR): XR_APILAYER_reshade registered for this user (files in {bed.LayerFolder()}) - it loads into EVERY OpenXR application until the last VR install is removed.", lines);
        Assert.Contains("outside: Windows graphics setting: Game.exe set to 'High performance' (the NVIDIA card, not the Intel one) for this user. A choice you already made there is kept. 'uninstall' takes it back out.", lines);
    }

    [TestMethod]
    public async Task A_reused_layer_is_previewed_as_never_removed()
    {
        using TransactionBed bed = NewGame();
        string foreign = Path.GetFullPath(Path.Join(bed.Tree.Root, "..", "ReShade", "ReShade32.json"));
        TestFiles.Plant(foreign, /*lang=json,strict*/ """{ "layer": { "library_path": "ReShade32.dll" } }""");
        _ = bed.User().Set(VulkanKey, foreign, 0);

        IReadOnlyList<string> lines = await PreviewAsync(bed, Bitness.Bits32, Layer(RegisteredLayer.VulkanLayer32));

        Assert.Contains($"outside: reuses the existing ReShade Vulkan layer ({foreign}); it is never removed by this tool", lines);
    }

    [TestMethod]
    public async Task The_preview_of_a_plan_with_a_layer_and_no_registry_names_the_refusal()
    {
        using RefusalBed bed = new();

        InstallCheck check = await bed.CheckAsync(LayerRequest(bed), Ct);

        check.AssertSays(BlockerKind.Plan, "cannot reach the registry or the layer folder it needs");
        Assert.IsNull(check.Resolved);
    }

    // A request for a plan registering the 64-bit Vulkan layer.
    private static RefusalRequest LayerRequest(RefusalBed bed) => bed.Request(bed.Bed.Plan(RouteId.Native, Layer(RegisteredLayer.VulkanLayer64)));

    // The preview of a plan of steps for a game of bitness, resolved against the bed's places.
    private async Task<IReadOnlyList<string>> PreviewAsync(TransactionBed bed, Bitness bitness, params PlanStep[] steps) =>
        InstallPreview.Of(new InstallCheck([], []) { Resolved = await bed.ResolveAsync(bed.Plan(bitness, steps), Ct) }).Lines();
}
