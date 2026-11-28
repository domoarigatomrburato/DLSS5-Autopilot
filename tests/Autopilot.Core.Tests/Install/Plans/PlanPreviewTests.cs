using Autopilot.Core.Install;
using Autopilot.Core.Install.Plans;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install.Plans;

/// <summary>Folders the plan tests share.</summary>
internal static class PlanFolders
{
    /// <summary>A bed whose game ships <c>nvngx_dlssd.dll</c>, installed once with ray reconstruction swapped.</summary>
    public static async Task<PlanBed> SwappedDlssdAsync(CancellationToken cancellationToken)
    {
        PlanBed bed = new(TreeFile.Bytes("nvngx_dlssd.dll", GameFolders.Dll("the game's dlssd"))) { Settings = RouteSettings.Default with { RayReconstructionLabel = "1.0" } };
        Assert.IsInstanceOfType<InstallCommitted>(await bed.InstallAsync(cancellationToken));
        return bed;
    }

    /// <summary>A bed over <see cref="Busy"/>, Windows' compiler newer than the game's, every swap asked for and an overlay key set.</summary>
    public static PlanBed BusyBed()
    {
        PlanBed bed = new PlanBed(Busy).WindowsCompiler("10.0.26100.1");
        bed.Settings = RouteSettings.Default with { KeepGameDlss = false, RayReconstructionLabel = "1.0", OverlayKey = 0x24 };
        return bed;
    }

    /// <summary>The blockers over a folder whose <c>dxgi.dll</c> is DXVK when <paramref name="what"/> says so, otherwise a plain DLL naming it.</summary>
    public static async Task<string> RefusedOverDxgiAsync(string what, CancellationToken cancellationToken)
    {
        using PlanBed bed = new(TreeFile.Bytes("dxgi.dll", what == "DXVK" ? Markers.Carrying(what) : GameFolders.Dll(what)));
        return await bed.RefusedAsync(cancellationToken);
    }

    // A folder every base rule acts on: a second ReShade, a feeder add-on, an older compiler, the person's ReShade.ini, the game's DLSS.
    public static TreeFile[] Busy =>
    [
        .. GameFolders.SecondReShade,
        TreeFile.Bytes("dlss5-feed.addon64", GameFolders.Dll("the feeder")),
        TreeFile.Bytes("d3dcompiler_47.dll", PlanRuns.Compiler("10.0.17763.1")),
        TreeFile.Text("ReShade.ini", "[GENERAL]\r\nEffectSearchPaths=.\\my-shaders\r\n"),
        TreeFile.Bytes("nvngx_dlss.dll", GameFolders.Dll("the game's dlss")),
        TreeFile.Bytes("nvngx_dlssd.dll", GameFolders.Dll("the game's dlssd")),
    ];
}

[TestClass]
public sealed class PlanPreviewTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_preview_of_a_Vulkan_game_lists_the_layer_without_its_bytes()
    {
        using PlanBed bed = new() { Facts = RouteFactsBeds.Game(Core.Graphics.GraphicsApi.Vulkan, executable: "Game.exe") };
        bed.WithLayerFolder();

        InstallPlan preview = await bed.PlanAsync(Ct, PreviewFiles.Instance);

        Assert.AreEqual("", await bed.RefusedAsync(preview, Ct));
        Assert.AreEqual("layer VulkanLayer64", preview.Group("Registering ReShade's Vulkan layer"));
    }

    [TestMethod]
    public async Task Special_K_under_the_proxy_name_refuses_the_install_even_though_it_names_ReShade()
    {
        using PlanBed bed = new(TreeFile.Bytes("dxgi.dll", Markers.SpecialKNamingReShade));

        StringAssert.StartsWith(await bed.RefusedAsync(Ct), "ProxyTaken: dxgi.dll already exists and is Special K, not ReShade.", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task The_preview_names_the_same_paths_offline_and_the_transaction_refuses_it()
    {
        using PlanBed bed = PlanFolders.BusyBed();

        InstallPlan gathered = await bed.PlanAsync(Ct);
        InstallPlan preview = await bed.PlanAsync(Ct, PreviewFiles.Instance);

        Assert.AreEqual(gathered.Lines(), preview.Lines());
        Assert.IsTrue(preview.NeedsGathering);
        Assert.IsFalse(gathered.NeedsGathering);
        Assert.AreEqual(await bed.PreviewedAsync(gathered, Ct), await bed.PreviewedAsync(preview, Ct));
    }

    [TestMethod]
    public async Task The_transaction_refuses_the_previews_plan_and_writes_nothing()
    {
        using PlanBed bed = new(PlanFolders.Busy);
        InstallPlan preview = await bed.PlanAsync(Ct, PreviewFiles.Instance);
        TreeSnapshot before = bed.Bed.Snapshot();

        _ = await Assert.ThrowsExactlyAsync<ArgumentException>(() => bed.Bed.InstallAsync(preview, Ct));

        before.AssertSame(bed.Bed.Snapshot());
    }

    [TestMethod]
    [DataRow("the game's own", "dxgi.dll already exists but is not ReShade (DXVK, Special K or another injector?). Remove it first, or choose another name under 'ReShade loads as', then install again.")]
    [DataRow("DXVK", "dxgi.dll already exists and is DXVK, not ReShade. Remove it first, or choose another name under 'ReShade loads as', then install again.")]
    public async Task A_proxy_name_taken_by_something_else_is_refused_before_anything_is_written(string what, string message) =>
        Assert.AreEqual("ProxyTaken: " + message, await PlanFolders.RefusedOverDxgiAsync(what, Ct));

    [TestMethod]
    public async Task A_ReShade_or_a_hand_installed_OptiScaler_under_the_proxy_name_is_not_a_refusal()
    {
        using PlanBed reshade = new(TreeFile.Bytes("dxgi.dll", Markers.ReShadeDll));
        using PlanBed optiScaler = new(GameFolders.HandInstalledOptiScaler);

        Assert.AreEqual("", await reshade.RefusedAsync(Ct));
        Assert.AreEqual("", await optiScaler.RefusedAsync(Ct));
    }

    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)] // Two full transactions over a folder holding a 1 MB ReShade.
    public async Task Installing_native_then_uninstalling_leaves_the_folder_byte_for_byte_as_it_was()
    {
        using PlanBed bed = PlanFolders.BusyBed();
        TreeSnapshot before = bed.Bed.Snapshot();

        Assert.IsInstanceOfType<InstallCommitted>(await bed.InstallAsync(Ct));
        Assert.IsNotNull(before.Difference(bed.Bed.Snapshot()));
        _ = await bed.Bed.UninstallFinishedAsync(Ct);

        before.AssertSame(bed.Bed.Snapshot());
    }
}
