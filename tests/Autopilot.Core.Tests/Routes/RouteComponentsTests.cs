using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;
using static Autopilot.Core.Tests.TestBeds.RouteFactsBeds;

namespace Autopilot.Core.Tests.Routes;

/// <summary>Which components each route installs, per routes.md's "What each route installs" and "Translation layers".</summary>
[TestClass]
public sealed class RouteComponentsTests
{
    [TestMethod]
    public void Native_installs_reshade_the_dlss5_add_on_and_the_runtimes() =>
        AssertIds(RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true), RouteSettings.Default), "reshade", "renodx", "dlssnr", "dlss");

    [TestMethod]
    public void Upstream_installs_its_add_on_and_no_nvngx_dlss() =>
        AssertIds(RouteComponents.For(RouteId.Upstream, Game(GraphicsApi.DX12, ownDlss: true), RouteSettings.Default), "reshade", "dlssnr", "upstream");

    [TestMethod]
    public void Bridge_installs_its_add_on_beside_the_dlss5_add_on() =>
        AssertIds(RouteComponents.For(RouteId.Bridge, Game(GraphicsApi.Vulkan), RouteSettings.Default), "reshade", "renodx", "dlssnr", "dlss", "bridge");

    [TestMethod]
    public void The_feeder_installs_the_headers_and_lumenites_kernel_by_default() =>
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), RouteSettings.Default), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "lumenite");

    [TestMethod]
    public void The_feeder_installs_the_providers_shaders_and_none_for_a_shader_the_person_installs()
    {
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), new RouteSettings { MotionProvider = MotionProvider.Vort }), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "vort");
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), new RouteSettings { MotionProvider = MotionProvider.BuiltIn }), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder");
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), new RouteSettings { MotionProvider = MotionProvider.Launchpad }), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder");
    }

    [TestMethod]
    public void An_opengl_game_on_the_feeder_takes_vort_never_lumenite()
    {
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.OpenGL), RouteSettings.Default), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "vort");
        Assert.AreEqual(MotionProvider.Vort, RouteComponents.ProviderFor(GraphicsApi.OpenGL, MotionProvider.LumeniteKernel));
        Assert.AreEqual(MotionProvider.BuiltIn, RouteComponents.ProviderFor(GraphicsApi.OpenGL, MotionProvider.BuiltIn));
        Assert.AreEqual(MotionProvider.LumeniteKernel, RouteComponents.ProviderFor(GraphicsApi.DX11, null));
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX9)]
    [DataRow(GraphicsApi.DX8)]
    public void A_32_bit_direct3d_9_or_8_game_on_the_feeder_goes_through_dxvk_box_or_not(GraphicsApi api) =>
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(api), new RouteSettings { Dxvk = false }), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder", "lumenite", "dxvk");

    [TestMethod]
    public void Direct3d_11_goes_through_dxvk_by_the_box_and_by_default_for_a_game_known_to_need_it()
    {
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX11), RouteSettings.Default));
        Assert.Contains("dxvk", RouteComponents.For(RouteId.Bridge, Game(GraphicsApi.DX11), new RouteSettings { Dxvk = true }));
        Assert.Contains("dxvk", RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX11, executable: @"C:\Games\MGSV\MGSVTPP.exe"), RouteSettings.Default));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX11, executable: "mgsvtpp.exe"), new RouteSettings { Dxvk = false }));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), new RouteSettings { Dxvk = true }), "DXVK serves Direct3D 8, 9 and 11 only");
    }

    [TestMethod]
    public void No_other_route_goes_through_dxvk()
    {
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Renodx, Game(GraphicsApi.DX9), new RouteSettings { Dxvk = true }));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Uplift, Game(GraphicsApi.DX11), new RouteSettings { Dxvk = true }));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX11), new RouteSettings { Dxvk = true }));
    }

    [TestMethod]
    public void Standalone_installs_its_add_on_the_headers_vort_and_frame_generation() =>
        AssertIds(RouteComponents.For(RouteId.Standalone, Game(GraphicsApi.DX11), RouteSettings.Default), "reshade", "shader_headers", "dlssnr", "dlss", "dlssg", "vort", "standalone");

    [TestMethod]
    public void Renodx_installs_shortfuses_add_on() =>
        AssertIds(RouteComponents.For(RouteId.Renodx, Game(GraphicsApi.DX9), RouteSettings.Default), "reshade", "renodx_sf", "dlssnr", "dlss");

    [TestMethod]
    public void Uplift_installs_its_add_on_and_no_nvngx_dlss() =>
        AssertIds(RouteComponents.For(RouteId.Uplift, Game(GraphicsApi.Vulkan), RouteSettings.Default), "reshade", "dlssnr", "uplift");

    [TestMethod]
    public void Optiscaler_installs_no_reshade_and_nvngx_dlss_only_for_a_game_without_its_own()
    {
        AssertIds(RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX12, ownDlss: true), RouteSettings.Default), "dlssnr", "optiscaler");
        AssertIds(RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX12), RouteSettings.Default), "dlssnr", "dlss", "optiscaler");
    }

    [TestMethod]
    public void Remix_installs_the_runtime_dll_and_the_community_runtime_only_when_swapped()
    {
        RouteFacts remix = Game(GraphicsApi.DX9) with { IsReEngine = true };

        AssertIds(RouteComponents.For(RouteId.Remix, remix, RouteSettings.Default), "dlssnr");
        AssertIds(RouteComponents.For(RouteId.Remix, remix, new RouteSettings { SwapRemixRuntime = true }), "dlssnr", "remix_runtime");
    }

    [TestMethod]
    public void An_re_engine_game_gets_reframework_on_every_route_but_remix()
    {
        RouteFacts capcom = Game(GraphicsApi.DX12, ownDlss: true) with { IsReEngine = true };

        Assert.Contains("reframework", RouteComponents.For(RouteId.Native, capcom, RouteSettings.Default));
        Assert.Contains("reframework", RouteComponents.For(RouteId.OptiScaler, capcom, RouteSettings.Default));
    }

    [TestMethod]
    public void Multi_frame_generation_adds_the_unlock_and_the_loader_on_direct3d_12_and_vulkan_only()
    {
        RouteSettings unlock = new() { MultiFrameGeneration = true };

        Assert.Contains("mfg", RouteComponents.For(RouteId.Native, Rtx40Game(GraphicsApi.DX12), unlock));
        Assert.Contains("asi_loader", RouteComponents.For(RouteId.Bridge, Rtx40Game(GraphicsApi.Vulkan), unlock));
        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.Feeder, Rtx40Game(GraphicsApi.DX11), unlock));
        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.OptiScaler, Rtx40Game(GraphicsApi.DX12), unlock));
        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.Native, Rtx40Game(GraphicsApi.DX12), RouteSettings.Default));
    }

    [TestMethod]
    public void A_setting_the_route_hides_is_not_read()
    {
        RouteSettings unlock = new() { MultiFrameGeneration = true };

        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.Native, Rtx40Game(GraphicsApi.DX12) with { Gpu = Card(0x10DE, "NVIDIA GeForce RTX 5080") }, unlock), "an RTX 50 has it natively");
        Assert.DoesNotContain("mfg", RouteComponents.For(RouteId.Native, Rtx40Game(GraphicsApi.DX12) with { LoaderNameFree = false }, unlock));
        Assert.DoesNotContain("lumenite", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12), new RouteSettings { MotionProvider = MotionProvider.LumeniteKernel }));
    }

    [TestMethod]
    public void Quantmotion_installs_no_shader_and_stays_itself_for_the_refusal()
    {
        AssertIds(RouteComponents.For(RouteId.Feeder, Game(GraphicsApi.DX12), new RouteSettings { MotionProvider = MotionProvider.QuantMotion }), "reshade", "shader_headers", "renodx", "dlssnr", "dlss", "feeder");
        Assert.AreEqual(MotionProvider.QuantMotion, RouteComponents.ProviderFor(GraphicsApi.OpenGL, MotionProvider.QuantMotion));
    }

    [TestMethod]
    public void Ray_reconstruction_is_a_swap_never_an_addition()
    {
        Assert.Contains("dlssd", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true, rayReconstruction: true), new RouteSettings { RayReconstructionLabel = "310.2.1" }));
        Assert.DoesNotContain("dlssd", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true), new RouteSettings { RayReconstructionLabel = "310.2.1" }));
        Assert.DoesNotContain("dlssd", RouteComponents.For(RouteId.Native, Game(GraphicsApi.DX12, ownDlss: true, rayReconstruction: true), RouteSettings.Default));
    }

    [TestMethod]
    public void A_game_known_to_need_dxvk_keeps_it_whatever_api_is_set_by_hand()
    {
        AssertIds(
            RouteComponents.For(RouteId.Native, Game(GraphicsApi.Vulkan, ownDlss: true, executable: "mgsvtpp.exe"), RouteSettings.Default),
            "reshade", "renodx", "dlssnr", "dlss", "dxvk");
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.Native, Game(GraphicsApi.Vulkan, executable: "mgsvtpp.exe"), new RouteSettings { Dxvk = false }));
        Assert.DoesNotContain("dxvk", RouteComponents.For(RouteId.OptiScaler, Game(GraphicsApi.DX11, ownDlss: true, executable: "mgsvtpp.exe"), RouteSettings.Default));
    }

    [TestMethod]
    public void The_executables_known_to_need_dxvk_are_named_by_file_name_in_any_case()
    {
        Assert.IsTrue(RouteComponents.NeedsDxvk("MGSGZS.EXE"));
        Assert.IsFalse(RouteComponents.NeedsDxvk("game.exe"));
    }

    private static void AssertIds(IReadOnlyList<string> actual, params string[] expected) =>
        Assert.AreEqual(string.Join(", ", expected), string.Join(", ", actual));
}
