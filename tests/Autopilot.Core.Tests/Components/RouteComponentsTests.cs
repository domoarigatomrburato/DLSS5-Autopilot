using Autopilot.Core.Components;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Tests.Components;

/// <summary>Which components each route installs, per routes.md's "What each route installs" and "Translation layers".</summary>
[TestClass]
public sealed class RouteComponentsTests
{
    [TestMethod]
    public void Native_installs_reshade_the_dlss5_add_on_and_the_runtimes() =>
        AssertIds(RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true)), "reshade", "renodx", "dlssnr", "dlss");

    [TestMethod]
    public void Upstream_installs_its_add_on_and_no_nvngx_dlss() =>
        AssertIds(RouteComponents.For(RouteId.Upstream, Game(GraphicsApi.DX12, ownDlss: true)), "reshade", "dlssnr", "upstream");

    [TestMethod]
    public void Bridge_installs_its_add_on_beside_the_dlss5_add_on() =>
        AssertIds(RouteComponents.For(RouteId.Bridge, Game(GraphicsApi.Vulkan)), "reshade", "renodx", "dlssnr", "dlss", "bridge");

    [TestMethod]
    public void The_feeder_installs_the_headers_and_lumenites_kernel_by_default() =>
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12)), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "lumenite");

    [TestMethod]
    public void The_feeder_installs_the_providers_shaders_and_none_for_a_shader_the_person_installs()
    {
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), provider: MotionProvider.Vort), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "vort");
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), provider: MotionProvider.BuiltIn), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder");
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), provider: MotionProvider.Launchpad), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder");
    }

    [TestMethod]
    public void An_opengl_game_on_the_feeder_takes_vort_never_lumenite()
    {
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.OpenGL)), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "vort");
        Assert.AreEqual(MotionProvider.Vort, RouteComponents.ProviderFor(GraphicsApi.OpenGL, MotionProvider.LumeniteQuantMotion));
        Assert.AreEqual(MotionProvider.BuiltIn, RouteComponents.ProviderFor(GraphicsApi.OpenGL, MotionProvider.BuiltIn));
        Assert.AreEqual(MotionProvider.LumeniteQuantMotion, RouteComponents.ProviderFor(GraphicsApi.DX11, MotionProvider.LumeniteQuantMotion));
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX9)]
    [DataRow(GraphicsApi.DX8)]
    public void A_32_bit_direct3d_9_or_8_game_on_the_feeder_goes_through_dxvk_box_or_not(GraphicsApi api) =>
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(api), dxvk: false), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "lumenite", "dxvk");

    [TestMethod]
    public void Direct3d_11_goes_through_dxvk_by_the_box_and_by_default_for_a_game_known_to_need_it()
    {
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX11)));
        Assert.Contains("dxvk", RouteComponents.For(RouteId.Bridge, Game(GraphicsApi.DX11), dxvk: true));
        Assert.Contains("dxvk", RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX11, executable: @"C:\Games\MGSV\MGSVTPP.exe")));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX11, executable: "mgsvtpp.exe"), dxvk: false));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), dxvk: true), "DXVK serves Direct3D 8, 9 and 11 only");
    }

    [TestMethod]
    public void No_other_route_goes_through_dxvk()
    {
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Renodx, Game(GraphicsApi.DX9), dxvk: true));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Uplift, Game(GraphicsApi.DX11), dxvk: true));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX11), dxvk: true));
    }

    [TestMethod]
    public void Standalone_installs_its_add_on_the_headers_vort_and_frame_generation() =>
        AssertIds(RouteComponents.For(RouteId.Standalone, Game(GraphicsApi.DX11)), "reshade", "shader_headers", "dlssnr", "dlss", "dlssg", "vort", "standalone");

    [TestMethod]
    public void Renodx_installs_shortfuses_add_on() =>
        AssertIds(RouteComponents.For(RouteId.Renodx, Game(GraphicsApi.DX9)), "reshade", "renodx_sf", "dlssnr", "dlss");

    [TestMethod]
    public void Uplift_installs_its_add_on_and_no_nvngx_dlss() =>
        AssertIds(RouteComponents.For(RouteId.Uplift, Game(GraphicsApi.Vulkan)), "reshade", "dlssnr", "uplift");

    [TestMethod]
    public void Optiscaler_installs_no_reshade_and_nvngx_dlss_only_for_a_game_without_its_own()
    {
        AssertIds(RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX12, ownDlss: true)), "dlssnr", "optiscaler");
        AssertIds(RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX12)), "dlssnr", "dlss", "optiscaler");
    }

    [TestMethod]
    public void Remix_installs_the_runtime_dll_and_the_community_runtime_only_when_swapped()
    {
        RouteFacts remix = Game(GraphicsApi.DX9) with { IsReEngine = true };

        AssertIds(RouteComponents.For(RouteId.Remix, remix), "dlssnr");
        AssertIds(RouteComponents.For(RouteId.Remix, remix, swapRemixRuntime: true), "dlssnr", "remix_runtime");
    }

    [TestMethod]
    public void An_re_engine_game_gets_reframework_on_every_route_but_remix()
    {
        RouteFacts capcom = Game(GraphicsApi.DX12, ownDlss: true) with { IsReEngine = true };

        Assert.Contains("reframework", RouteComponents.For(RouteId.Native, capcom));
        Assert.Contains("reframework", RouteComponents.For(RouteId.OptiScaler, capcom));
    }

    [TestMethod]
    public void Multi_frame_generation_adds_the_unlock_and_the_loader_on_direct3d_12_and_vulkan_only()
    {
        Assert.Contains("mfg", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12), multiFrameGeneration: true));
        Assert.Contains("asi_loader", RouteComponents.For(RouteId.Bridge, Game(GraphicsApi.Vulkan), multiFrameGeneration: true));
        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX11), multiFrameGeneration: true));
        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX12), multiFrameGeneration: true));
        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12)));
    }

    [TestMethod]
    public void Ray_reconstruction_is_a_swap_never_an_addition()
    {
        Assert.Contains("dlssd", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true, rayReconstruction: true), rayReconstruction: true));
        Assert.DoesNotContain("dlssd", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true), rayReconstruction: true));
        Assert.DoesNotContain("dlssd", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true, rayReconstruction: true)));
    }

    [TestMethod]
    public void A_game_known_to_need_dxvk_keeps_it_whatever_api_is_set_by_hand()
    {
        AssertIds(
            RouteComponents.For(RouteId.Native, Game(GraphicsApi.Vulkan, ownDlss: true, executable: "mgsvtpp.exe")),
            "reshade", "renodx", "dlssnr", "dlss", "dxvk");
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Native, Game(GraphicsApi.Vulkan, executable: "mgsvtpp.exe"), dxvk: false));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX11, ownDlss: true, executable: "mgsvtpp.exe")));
    }

    [TestMethod]
    public void The_executables_known_to_need_dxvk_are_named_by_file_name_in_any_case()
    {
        Assert.IsTrue(RouteComponents.NeedsDxvk("MGSGZS.EXE"));
        Assert.IsFalse(RouteComponents.NeedsDxvk("game.exe"));
    }

    private static RouteFacts Game(GraphicsApi api, bool ownDlss = false, bool rayReconstruction = false, string executable = "game.exe") =>
        new(api, new(ownDlss, [], Upscaler.None, [], rayReconstruction, false, null), false, executable);

    private static void AssertIds(IReadOnlyList<string> actual, params string[] expected) =>
        Assert.AreEqual(string.Join(", ", expected), string.Join(", ", actual));
}
