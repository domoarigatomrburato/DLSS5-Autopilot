using Autopilot.Core.Graphics;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>A d3d9.dll import is the weakest evidence: every other kind is asked first.</summary>
[TestClass]
public sealed class D3d9SequenceTests(TestContext testContext) : GraphicsTestBase(testContext)
{
    [TestMethod]
    public async Task A_game_with_nothing_but_d3d9_is_directx_9() =>
        Is(await DecideAsync(Game(imports: ["d3d9.dll", "kernel32.dll"])), GraphicsApi.DX9, ApiReason.StaticImport, "imports d3d9.dll, no DXGI");

    [TestMethod]
    public async Task Gta_v_with_a_d3d9_import_and_nothing_else_is_directx_9() =>
        Is(await DecideAsync(Game("GTA5.exe", imports: "d3d9.dll")), GraphicsApi.DX9, ApiReason.StaticImport);

    [TestMethod]
    public async Task Red_dead_redemption_2_importing_d3d9_but_shipping_dlss_is_dx12()
    {
        string exe = Game("RDR2.exe", imports: "d3d9.dll");
        Dll(exe, "nvngx_dlss.dll");

        ApiDecision decision = await DecideAsync(exe);

        Is(decision, GraphicsApi.DX12, ApiReason.DlssFiles, "ships nvngx_dlss.dll", "set to Vulkan");
        Assert.AreEqual(ApiConfidence.Inferred, decision.Confidence);
    }

    [TestMethod]
    public async Task A_game_whose_dlss_file_is_not_an_executable_still_ships_dlss()
    {
        string exe = Game(imports: "d3d9.dll");
        Junk(exe, "nvngx_dlss.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.DlssFiles);
    }

    [TestMethod]
    public async Task A_d3d9_game_with_an_agility_sdk_is_dx12()
    {
        string exe = Game(imports: "d3d9.dll");
        Dll(exe, Path.Join("D3D12", "D3D12Core.dll"));

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk, "D3D12Core.dll", "set to Vulkan");
    }

    [TestMethod]
    public async Task A_delay_loaded_d3d11_is_the_renderer_and_the_reason_says_so() =>
        Is(await DecideAsync(Write(new TestPe().WithImports("d3d9.dll").WithDelayImports("d3d11.dll"))), GraphicsApi.DX11, ApiReason.DelayLoad, "delay-loads d3d11.dll", "Direct3D 11");

    [TestMethod]
    public async Task A_delay_loaded_d3d12_outranks_a_delay_loaded_d3d11() =>
        Is(await DecideAsync(Write(new TestPe(Bits32).WithImports("d3d9.dll").WithDelayImports("d3d11.dll", "d3d12.dll"))), GraphicsApi.DX12, ApiReason.DelayLoad, "d3d12.dll");

    [TestMethod]
    public async Task A_bare_dxgi_delay_load_does_not_make_a_directx_9_game_modern() =>
        Is(await DecideAsync(Write(new TestPe().WithImports("d3d9.dll").WithDelayImports("dxgi.dll"))), GraphicsApi.DX9, ApiReason.StaticImport);

    [TestMethod]
    public async Task Shipped_dlss_is_asked_before_the_delay_load_table()
    {
        string exe = Write(new TestPe().WithImports("d3d9.dll").WithDelayImports("d3d11.dll"));
        Dll(exe, "nvngx_dlss.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.DlssFiles);
    }

    [TestMethod]
    public async Task Frame_generation_beside_a_foreign_dlss_tool_says_nothing_about_a_d3d9_game()
    {
        string exe = Game(imports: "d3d9.dll");
        Dll(exe, "nvngx_dlssg.dll");
        Junk(exe, "OptiScaler.ini", "[x]");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_d3d9_game_keeps_its_own_d3d9_name_over_a_sibling_that_imports_d3d12()
    {
        // The import table's own name string puts d3d9.dll in the exe's bytes, and the exe's strings come first.
        string exe = Game(imports: "d3d9.dll");
        Dll(exe, "renderer.dll", Bits64, "d3d12.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_32_bit_d3d9_game_never_asks_the_run_time_search()
    {
        string exe = Game(machine: Bits32, imports: "d3d9.dll");
        Dll(exe, "renderer.dll", Bits32, "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_sibling_that_only_names_d3d11_in_its_bytes_does_not_rescue_a_d3d9_game()
    {
        string exe = Game(imports: "d3d9.dll");
        NamingDll(exe, "engine.dll", Bits64, 600_000, "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_d3d11_name_in_the_exe_bytes_does_not_make_an_sdl_game_dx11() =>
        Is(await DecideAsync(Naming("Game.exe", Bits64, ["d3d9.dll"], "d3d11.dll")), GraphicsApi.DX9, ApiReason.StaticImport);

    [TestMethod]
    public async Task A_64_bit_d3d9_unity_game_is_dx11()
    {
        string exe = Game(imports: "d3d9.dll");
        Junk(exe, "UnityPlayer.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.Unity, "imports d3d9.dll", "Unity");
    }

    [TestMethod]
    public async Task A_64_bit_d3d9_game_in_an_unreal_layout_is_direct3d_11_or_12()
    {
        Folder(Work, "Engine");
        string exe = Game(Path.Join("Game", "Binaries", "Win64", "Game.exe"), imports: "d3d9.dll");

        Is(await DecideAsync(exe), GraphicsApi.Dxgi, ApiReason.Unreal, "imports d3d9.dll", "Unreal Engine");
    }

    [TestMethod]
    public async Task A_d3d9_game_too_large_to_search_whose_sibling_imports_d3d12_is_dx12()
    {
        string exe = Write(new TestPe { Size = 600L * 1024 * 1024 }.WithImports("d3d9.dll"));
        Dll(exe, "renderer.dll", Bits64, "d3d12.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.SiblingImport, "imports d3d9.dll", "loads d3d12.dll at run time", "renderer.dll beside the exe imports it", "Direct3D 12");
    }
}
