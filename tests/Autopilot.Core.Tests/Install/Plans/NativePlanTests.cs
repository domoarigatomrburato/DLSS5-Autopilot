using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install.Plans;

[TestClass]
public sealed class NativePlanTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Native_is_the_ReShade_base_and_Krishs_add_on_with_the_groups_in_a_fixed_order()
    {
        using PlanBed bed = new PlanBed(PlanFolders.Busy).WindowsCompiler("10.0.26100.1").WithTwoCards();
        bed.Settings = RouteSettings.Default with { KeepGameDlss = false, RayReconstructionLabel = "1.0" };

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual(
            """
            Taking other add-ons and ReShade copies out of the way
            Moving the game's older Direct3D compiler aside
            Placing ReShade
            Writing ReShade.ini
            Placing nvngx_dlssnr.dll
            Placing nvngx_dlss.dll
            Swapping nvngx_dlssd.dll
            Placing the renodx-dlss5 add-on
            Setting Windows' graphics preference
            """.ReplaceLineEndings("\n"),
            plan.GroupNames());
    }

    [TestMethod]
    public async Task The_header_names_each_components_version_and_the_settings_as_read()
    {
        using PlanBed bed = new() { Settings = RouteSettings.Default with { OptiScalerPasses = NeuralPasses.Auto } };

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual("1.0", plan.Header.Components["renodx"]);
        Assert.AreEqual("1.0", plan.Header.Components["reshade"]);
        Assert.AreEqual(RouteId.Native, plan.Header.Route);
        Assert.IsNull(plan.Header.Choices.OptiScalerPasses);
    }

    [TestMethod]
    public async Task Routes_not_written_yet_and_native_through_DXVK_are_refused_whole()
    {
        using PlanBed bed = new();
        using PlanBed dxvk = new() { Facts = RouteFactsBeds.Game(GraphicsApi.DX9, executable: "Game.exe"), Settings = RouteSettings.Default with { Dxvk = true } };

        _ = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => bed.PlanAsync(Ct, route: RouteId.Upstream));
        _ = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => dxvk.PlanAsync(Ct));
    }
}
