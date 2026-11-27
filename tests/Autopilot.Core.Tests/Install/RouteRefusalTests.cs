using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The refusals the game, the route, the settings and the card decide, per routes.md's "Pre-install checks and warnings".</summary>
[TestClass]
public sealed class RouteRefusalTests(TestContext testContext)
{
    private static readonly RouteSettings _quantMotion = new() { MotionProvider = MotionProvider.QuantMotion };

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_game_without_an_executable_is_refused_with_what_to_do()
    {
        using RefusalBed bed = new();

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native) with { Game = bed.Game with { Executable = null } }, Ct);

        check.AssertSays(BlockerKind.Game, "No game executable found. Nothing was installed; set it in the game's settings");
    }

    [TestMethod]
    [DataRow(GraphicsApi.Unknown)]
    [DataRow(GraphicsApi.Dxgi)]
    public async Task An_api_not_settled_is_refused_until_the_person_chooses_it(GraphicsApi api)
    {
        using RefusalBed bed = new();

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native) with { Facts = RefusalGames.Facts with { Api = api } }, Ct);

        check.AssertSays(BlockerKind.Api, "Choose it under 'graphics api'");
    }

    [TestMethod]
    public async Task A_card_dlss_cannot_run_on_is_refused_unless_the_person_installs_anyway()
    {
        using RefusalBed bed = new();
        RefusalRequest request = bed.Request(RouteId.Native) with { Facts = OnCard(RouteFactsBeds.Card(0x1002, "AMD Radeon RX 7900 XTX")) };

        (await bed.CheckAsync(request, Ct)).AssertSays(BlockerKind.Card, "To install anyway, choose 'install anyway'.");
        (await bed.CheckAsync(request with { IgnoreGpuMismatch = true }, Ct)).AssertAllowed();
    }

    [TestMethod]
    public async Task The_rtx_40_mfg_package_is_refused_on_any_other_card()
    {
        using RefusalBed bed = new();
        RefusalRequest request = bed.Request(RouteId.OptiScaler, new RouteSettings { OptiScalerBuild = OptiScalerBuildKey.Wilsjo2Mfg });

        (await bed.CheckAsync(request with { Facts = OnCard(RouteFactsBeds.Card(0x10DE, "NVIDIA GeForce RTX 5090")) }, Ct)).AssertSays(BlockerKind.OptiScalerBuild, "Choose \"wilsjo2's fork\"");
        (await bed.CheckAsync(request with { Facts = OnCard(RouteFactsBeds.Card(0x10DE, "NVIDIA GeForce RTX 4070")) }, Ct)).AssertAllowed();
    }

    [TestMethod]
    public async Task Provider_4_stops_the_install_and_says_why()
    {
        using RefusalBed bed = new();

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Feeder, new RouteSettings { MotionProvider = MotionProvider.QuantMotion }), Ct);

        Assert.AreEqual("LumeniteFX renamed QuantMotion to PocketMV and the feeder does not read it yet; pick provider 3 or 5.", check.Only(BlockerKind.MotionProvider));
    }

    [TestMethod]
    public async Task Provider_4_named_by_a_record_stops_the_install_it_repeats()
    {
        using RefusalBed bed = new();
        Recorded.Plant(bed.Tree.Root, Recorded.Install() with { Choices = _quantMotion });
        RouteSettings recorded = (await Recorded.FindAsync(bed.Tree.Root, Ct))!.Record!.Choices;

        _ = (await bed.CheckAsync(bed.Request(RouteId.Feeder, recorded), Ct)).Only(BlockerKind.MotionProvider);
    }

    [TestMethod]
    public async Task A_hidden_setting_is_not_read()
    {
        using RefusalBed bed = new();

        (await bed.CheckAsync(bed.Request(RouteId.Native, new RouteSettings { MotionProvider = MotionProvider.QuantMotion }), Ct)).AssertAllowed();
    }

    [TestMethod]
    public void A_feeder_build_older_than_the_game_or_the_settings_need_is_refused_naming_the_build_to_pick()
    {
        RouteSettings helperBuiltIn = new() { HelperProcess = true, MotionProvider = MotionProvider.BuiltIn };

        Assert.HasCount(3, GatheredRefusals.FeederBuild(new ComponentVersion("v0.12.0"), GraphicsApi.DX10, helperBuiltIn));
        Assert.IsEmpty(GatheredRefusals.FeederBuild(new ComponentVersion("v2.0.0"), GraphicsApi.DX10, helperBuiltIn));
        Assert.IsEmpty(GatheredRefusals.FeederBuild(new ComponentVersion("v0.12.0"), GraphicsApi.DX12, RouteSettings.Default));
        Assert.Contains("v1.18.0-beta.1", GatheredRefusals.FeederBuild(new ComponentVersion("v1.17.0"), GraphicsApi.DX11, new RouteSettings { HelperProcess = true })[0].Text);
    }

    [TestMethod]
    public void Uplift_on_vulkan_or_opengl_needs_reshade_6_8()
    {
        Assert.AreEqual(BlockerKind.UpliftReShade, GatheredRefusals.UpliftReShade(new ComponentVersion("6.7.3"), GraphicsApi.Vulkan)?.Kind);
        Assert.Contains("ReShade 6.8.0 or newer", GatheredRefusals.UpliftReShade(new ComponentVersion("6.7.3"), GraphicsApi.OpenGL)!.Text);
        Assert.IsNull(GatheredRefusals.UpliftReShade(new ComponentVersion("6.8.0"), GraphicsApi.Vulkan));
        Assert.IsNull(GatheredRefusals.UpliftReShade(new ComponentVersion("6.7.3"), GraphicsApi.DX12));
    }

    private static RouteFacts OnCard(Core.Gpu.GpuFacts card) => RefusalGames.Facts with { Gpu = card };
}
