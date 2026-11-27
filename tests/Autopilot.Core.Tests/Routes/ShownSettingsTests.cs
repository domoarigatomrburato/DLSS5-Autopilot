using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;
using static Autopilot.Core.Tests.TestBeds.RouteFactsBeds;

namespace Autopilot.Core.Tests.Routes;

/// <summary>Which settings a game's page shows per route, and the reset of the hidden ones, per routes.md's "Per-route settings the person sees".</summary>
[TestClass]
public sealed class ShownSettingsTests
{
    private static readonly RouteId[] _reShadeRoutes = [RouteId.Native, RouteId.Upstream, RouteId.Bridge, RouteId.Feeder, RouteId.Standalone, RouteId.Renodx, RouteId.Uplift];

    [TestMethod]
    public void The_route_the_api_and_the_nvngx_dlssnr_build_show_on_every_route()
    {
        foreach (RouteId route in Enum.GetValues<RouteId>())
        {
            IReadOnlySet<RouteSetting> shown = Shown(route, Game());

            Assert.IsTrue(shown.IsSupersetOf([RouteSetting.Route, RouteSetting.GraphicsApi, RouteSetting.DlssnrBuild]), route.ToString());
        }
    }

    [TestMethod]
    public void The_architecture_shows_when_the_executable_could_not_be_read_and_the_executable_when_there_are_several()
    {
        Assert.DoesNotContain(RouteSetting.Architecture, Shown(RouteId.Native, Game()));
        Assert.Contains(RouteSetting.Architecture, Shown(RouteId.Native, Game() with { ArchitectureRead = false }));
        Assert.DoesNotContain(RouteSetting.Executable, Shown(RouteId.Native, Game()));
        Assert.Contains(RouteSetting.Executable, Shown(RouteId.Native, Game() with { Executables = 2 }));
    }

    [TestMethod]
    public void Optiscalers_settings_show_on_optiscaler_only()
    {
        RouteSetting[] optiScaler = [RouteSetting.OptiScalerLoadsAs, RouteSetting.OptiScalerBuild, RouteSetting.OptiScalerPreset, RouteSetting.OptiScalerStyle, RouteSetting.ModelResolution];

        Assert.IsTrue(Shown(RouteId.OptiScaler, Game()).IsSupersetOf(optiScaler));
        Assert.IsFalse(Shown(RouteId.Native, Game()).Overlaps(optiScaler));
    }

    [TestMethod]
    [DataRow(null, false)]
    [DataRow(OptiScalerBuildKey.Y4my4my4m, false)]
    [DataRow(OptiScalerBuildKey.Wilsjo2, true)]
    [DataRow(OptiScalerBuildKey.Wilsjo2Mfg, true)]
    [DataRow(OptiScalerBuildKey.Janblade, true)]
    [DataRow(OptiScalerBuildKey.Jlrouzies, true)]
    public void Passes_show_for_the_builds_that_run_before_super_resolution(OptiScalerBuildKey? build, bool shown) =>
        Assert.AreEqual(shown, Shown(RouteId.OptiScaler, Game(), new RouteSettings { OptiScalerBuild = build }).Contains(RouteSetting.OptiScalerPasses));

    [TestMethod]
    public void The_feeders_work_resolution_shows_on_a_64_bit_direct3d_11_game_drawing_itself()
    {
        Assert.Contains(RouteSetting.ModelResolution, Shown(RouteId.Feeder, Game(GraphicsApi.DX11)));
        Assert.DoesNotContain(RouteSetting.ModelResolution, Shown(RouteId.Feeder, Game(GraphicsApi.DX11), new RouteSettings { Dxvk = true }), "through DXVK");
        Assert.DoesNotContain(RouteSetting.ModelResolution, Shown(RouteId.Feeder, Game(GraphicsApi.DX11), new RouteSettings { HelperProcess = true }), "in the helper");
        Assert.DoesNotContain(RouteSetting.ModelResolution, Shown(RouteId.Feeder, Game(GraphicsApi.DX12)));
        Assert.DoesNotContain(RouteSetting.ModelResolution, Shown(RouteId.Feeder, Game(GraphicsApi.DX11) with { Bitness = Bitness.Bits32 }));
    }

    [TestMethod]
    public void Frame_generation_and_own_files_show_on_optiscaler_on_direct3d_12_and_forget_only_with_a_set_chosen()
    {
        Assert.IsTrue(Shown(RouteId.OptiScaler, Game()).IsSupersetOf([RouteSetting.FrameGeneration, RouteSetting.OwnFrameGeneration]));
        Assert.DoesNotContain(RouteSetting.ForgetOwnFrameGeneration, Shown(RouteId.OptiScaler, Game()));
        Assert.Contains(RouteSetting.ForgetOwnFrameGeneration, Shown(RouteId.OptiScaler, Game(), new RouteSettings { OwnFrameGeneration = OwnFrameGeneration.Enabler }));
        Assert.IsFalse(Shown(RouteId.OptiScaler, Game(GraphicsApi.DX11)).Overlaps([RouteSetting.FrameGeneration, RouteSetting.OwnFrameGeneration]));
    }

    [TestMethod]
    public void The_feeders_settings_show_on_the_feeder_and_the_helper_on_a_64_bit_game()
    {
        RouteSetting[] feeder = [RouteSetting.MotionProvider, RouteSetting.FeederPreset, RouteSetting.FeederHdr, RouteSetting.FeederBuild, RouteSetting.HelperProcess];

        Assert.IsTrue(Shown(RouteId.Feeder, Game()).IsSupersetOf(feeder));
        Assert.DoesNotContain(RouteSetting.HelperProcess, Shown(RouteId.Feeder, Game() with { Bitness = Bitness.Bits32 }));
        Assert.IsFalse(Shown(RouteId.Bridge, Game()).Overlaps(feeder));
    }

    [TestMethod]
    [DataRow(RouteId.Native, GraphicsApi.DX12, true)]
    [DataRow(RouteId.Bridge, GraphicsApi.DX11, true)]
    [DataRow(RouteId.Feeder, GraphicsApi.OpenGL, true)]
    [DataRow(RouteId.Feeder, GraphicsApi.Vulkan, false)]
    [DataRow(RouteId.Renodx, GraphicsApi.DX9, false)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX8, false)]
    [DataRow(RouteId.OptiScaler, GraphicsApi.DX12, false)]
    [DataRow(RouteId.Remix, GraphicsApi.DX9, false)]
    public void Reshade_loads_as_shows_on_a_reshade_route_that_places_a_proxy(RouteId route, GraphicsApi api, bool shown) =>
        Assert.AreEqual(shown, Shown(route, Game(api)).Contains(RouteSetting.ReShadeLoadsAs));

    [TestMethod]
    public void Reshade_loads_as_hides_while_the_game_goes_through_dxvk()
    {
        Assert.DoesNotContain(RouteSetting.ReShadeLoadsAs, Shown(RouteId.Native, Game(GraphicsApi.DX11), new RouteSettings { Dxvk = true }));
        Assert.DoesNotContain(RouteSetting.ReShadeLoadsAs, Shown(RouteId.Native, Game(GraphicsApi.DX11) with { ExecutableName = "mgsvtpp.exe" }), "DXVK by default");
    }

    [TestMethod]
    public void The_dlss5_add_ons_build_shows_where_the_route_loads_it() =>
        Assert.AreEqual(
            "Native, Bridge, Feeder, Renodx",
            string.Join(", ", Enum.GetValues<RouteId>().Where(route => Shown(route, Game()).Contains(RouteSetting.RenodxBuild))));

    [TestMethod]
    public void The_nvngx_dlss_build_and_keep_show_where_it_is_placed()
    {
        Assert.AreEqual(
            "Native, OptiScaler, Bridge, Feeder, Standalone, Renodx",
            string.Join(", ", Enum.GetValues<RouteId>().Where(route => Shown(route, Game()).Contains(RouteSetting.DlssBuild))));
        Assert.DoesNotContain(RouteSetting.DlssBuild, Shown(RouteId.Native, Game() with { DlssBesideExecutable = true }), "the game's own is kept");
        Assert.Contains(RouteSetting.DlssBuild, Shown(RouteId.Native, Game() with { DlssBesideExecutable = true }, new RouteSettings { KeepGameDlss = false }));
        Assert.Contains(RouteSetting.KeepGameDlss, Shown(RouteId.Native, Game()));
        Assert.DoesNotContain(RouteSetting.KeepGameDlss, Shown(RouteId.Feeder, Game() with { Bitness = Bitness.Bits32 }));
        Assert.DoesNotContain(RouteSetting.KeepGameDlss, Shown(RouteId.Upstream, Game()));
        Assert.DoesNotContain(RouteSetting.DlssBuild, Shown(RouteId.OptiScaler, Game(ownDlss: true)), "OptiScaler on a game with DLSS places none: the game keeps its own");
    }

    [TestMethod]
    public void Ray_reconstruction_shows_on_a_reshade_route_for_a_game_shipping_its_runtime()
    {
        RouteFacts ships = Game() with { Runtimes = Game().Runtimes with { ShipsRayReconstruction = true } };

        Assert.Contains(RouteSetting.RayReconstruction, Shown(RouteId.Native, ships));
        Assert.DoesNotContain(RouteSetting.RayReconstruction, Shown(RouteId.OptiScaler, ships));
        Assert.DoesNotContain(RouteSetting.RayReconstruction, Shown(RouteId.Native, Game()));
    }

    [TestMethod]
    public void Dxvk_shows_on_native_bridge_and_feeder_for_direct3d_11_or_a_game_known_to_need_it()
    {
        Assert.Contains(RouteSetting.Dxvk, Shown(RouteId.Bridge, Game(GraphicsApi.DX11)));
        Assert.Contains(RouteSetting.Dxvk, Shown(RouteId.Native, Game() with { ExecutableName = "mgsgzs.exe" }));
        Assert.DoesNotContain(RouteSetting.Dxvk, Shown(RouteId.Native, Game()));
        Assert.DoesNotContain(RouteSetting.Dxvk, Shown(RouteId.Renodx, Game(GraphicsApi.DX11)));
    }

    [TestMethod]
    public void Multi_frame_generation_shows_on_an_rtx_40_for_a_game_with_frame_generation_and_a_free_loader_name()
    {
        RouteFacts fits = Game() with { Gpu = Card(0x10DE, "NVIDIA GeForce RTX 4070"), LoaderNameFree = true, Runtimes = Game().Runtimes with { ShipsFrameGeneration = true } };

        Assert.Contains(RouteSetting.MultiFrameGeneration, Shown(RouteId.Native, fits));
        Assert.Contains(RouteSetting.MultiFrameGeneration, Shown(RouteId.Bridge, fits with { Api = GraphicsApi.Vulkan }));
        Assert.DoesNotContain(RouteSetting.MultiFrameGeneration, Shown(RouteId.Native, fits with { Gpu = Card(0x10DE, "NVIDIA GeForce RTX 5080") }));
        Assert.DoesNotContain(RouteSetting.MultiFrameGeneration, Shown(RouteId.Native, fits with { LoaderNameFree = false }));
        Assert.DoesNotContain(RouteSetting.MultiFrameGeneration, Shown(RouteId.Native, fits with { Runtimes = Game().Runtimes }));
        Assert.DoesNotContain(RouteSetting.MultiFrameGeneration, Shown(RouteId.Feeder, fits with { Api = GraphicsApi.DX11 }));
        Assert.DoesNotContain(RouteSetting.MultiFrameGeneration, Shown(RouteId.OptiScaler, fits));
    }

    [TestMethod]
    public void The_overlay_key_shows_on_every_route_but_remix_and_the_runtime_swap_on_remix_only()
    {
        foreach (RouteId route in Enum.GetValues<RouteId>())
        {
            IReadOnlySet<RouteSetting> shown = Shown(route, Game());

            Assert.AreEqual(route != RouteId.Remix, shown.Contains(RouteSetting.OverlayKey), route.ToString());
            Assert.AreEqual(route == RouteId.Remix, shown.Contains(RouteSetting.SwapRemixRuntime), route.ToString());
        }
    }

    [TestMethod]
    public void The_vr_headset_shows_on_a_reshade_route_for_a_64_bit_game()
    {
        Assert.IsTrue(_reShadeRoutes.All(route => Shown(route, Game()).Contains(RouteSetting.VrHeadset)));
        Assert.DoesNotContain(RouteSetting.VrHeadset, Shown(RouteId.Feeder, Game() with { Bitness = Bitness.Bits32 }));
        Assert.DoesNotContain(RouteSetting.VrHeadset, Shown(RouteId.OptiScaler, Game()));
    }

    [TestMethod]
    public void The_windows_graphics_preference_shows_on_a_two_gpu_pc()
    {
        Assert.DoesNotContain(RouteSetting.GraphicsPreference, Shown(RouteId.Native, Game()));
        Assert.Contains(RouteSetting.GraphicsPreference, Shown(RouteId.Native, Game() with { Gpu = TwoCards }));
    }

    [TestMethod]
    public void A_route_change_resets_what_the_new_route_hides_and_keeps_what_it_shows()
    {
        RouteSettings onOptiScaler = new()
        {
            OptiScalerBuild = OptiScalerBuildKey.Wilsjo2,
            OptiScalerPasses = NeuralPasses.Three,
            ModelResolution = 50,
            FrameGeneration = true,
            DlssnrLabel = "310.4.0",
            OverlayKey = 0x24,
            UseNvidiaCard = false,
        };

        RouteSettings onNative = ShownSettings.Reset(RouteId.Native, Game(), onOptiScaler);

        Assert.AreEqual(new RouteSettings { DlssnrLabel = "310.4.0", OverlayKey = 0x24 }, onNative);
    }

    [TestMethod]
    public void A_reset_that_hides_another_setting_resets_that_one_too()
    {
        // A 32-bit game hides "keep the game's own", whose default (on) hides the build for a game whose own file sits beside the executable.
        RouteSettings swapped = new() { KeepGameDlss = false, DlssLabel = "310.3.0", MotionProvider = MotionProvider.Vort };
        RouteFacts game = Game() with { Bitness = Bitness.Bits32, DlssBesideExecutable = true };

        Assert.AreEqual(new RouteSettings { MotionProvider = MotionProvider.Vort }, ShownSettings.Reset(RouteId.Feeder, game, swapped));
        Assert.AreEqual(RouteSettings.Default, ShownSettings.Reset(RouteId.Remix, game, swapped));
    }

    [TestMethod]
    public void The_settings_a_route_shows_are_never_reset() =>
        Assert.AreEqual(FullChoicesOnFeeder, ShownSettings.Reset(RouteId.Feeder, Game(GraphicsApi.DX11), FullChoicesOnFeeder));

    // What the feeder on a 64-bit Direct3D 11 game shows, each away from its default.
    private static RouteSettings FullChoicesOnFeeder => new()
    {
        ModelResolution = 80,
        MotionProvider = MotionProvider.Vort,
        FeederPreset = DlssPreset.K,
        FeederHdr = FeederHdr.Sdr,
        FeederBuild = FeederChoice.NewestPrerelease,
        Dxvk = false,
        ReShadeLoadsAs = ReShadeProxy.D3d11,
        RenodxLabel = "4.55",
        DlssnrLabel = "310.4.0",
        DlssLabel = "310.3.0",
        KeepGameDlss = false,
        OverlayKey = 0x2D,
        VrHeadset = true,
    };

    private static IReadOnlySet<RouteSetting> Shown(RouteId route, RouteFacts facts, RouteSettings? settings = null) =>
        ShownSettings.For(route, facts, settings ?? RouteSettings.Default);
}
