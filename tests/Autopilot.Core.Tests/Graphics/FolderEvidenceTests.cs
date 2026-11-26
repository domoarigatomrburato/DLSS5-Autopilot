using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>Files beside the executable: limited by bitness, never ours, never another DLSS tool's.</summary>
[TestClass]
public sealed class FolderEvidenceTests(TestContext testContext) : GraphicsTestBase(testContext)
{
    [TestMethod]
    public async Task Mass_effect_andromeda_importing_d3d11_with_an_agility_sdk_is_dx12()
    {
        string exe = Game("MassEffectAndromeda.exe", imports: "d3d11.dll");
        Dll(exe, Path.Join("D3D12", "D3D12Core.dll"));

        ApiDecision decision = await DecideAsync(exe);

        Is(decision, GraphicsApi.DX12, ApiReason.AgilitySdk, "imports d3d11.dll", "D3D12Core.dll");
        Assert.AreEqual(ApiConfidence.Inferred, decision.Confidence);
    }

    [TestMethod]
    [DataRow("nvngx_dlssg.dll")]
    [DataRow("nvngx_dlssd.dll")]
    public async Task A_d3d11_game_shipping_frame_generation_or_ray_reconstruction_is_dx12(string runtime)
    {
        string exe = Game(imports: "d3d11.dll");
        Dll(exe, runtime);

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk, runtime);
    }

    [TestMethod]
    public async Task A_d3d11_game_shipping_only_dlss_super_resolution_stays_dx11()
    {
        string exe = Game(imports: "d3d11.dll");
        Dll(exe, "nvngx_dlss.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task Mass_effect_andromeda_with_our_standalone_frame_generation_stays_dx11()
    {
        string exe = Game("MassEffectAndromeda.exe", imports: "d3d11.dll");
        Dll(exe, "nvngx_dlssg.dll");
        Record(exe, "nvngx_dlssg.dll", "dxgi.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_file_of_ours_that_replaced_the_games_own_still_counts_as_the_games()
    {
        string exe = Game(imports: "d3d11.dll");
        Dll(exe, "nvngx_dlssg.dll");
        Dll(exe, "nvngx_dlssg.dll.dlss5-autopilot-backup");
        Record(exe, "sub\\NVNGX_DLSSG.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk);
    }

    [TestMethod]
    [DataRow("{ not json")]
    [DataRow("[\"nvngx_dlssg.dll\"]")]
    [DataRow(/*lang=json,strict*/ "{\"files\": \"nvngx_dlssg.dll\"}")]
    public async Task A_bad_install_record_means_nothing_is_ours(string record)
    {
        string exe = Game(imports: "d3d11.dll");
        Dll(exe, "nvngx_dlssg.dll");
        Junk(exe, "dlss5-autopilot.json", record);

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk);
    }

    [TestMethod]
    public async Task An_install_record_over_a_mebibyte_is_not_read()
    {
        string exe = Game(imports: "d3d11.dll");
        Dll(exe, "nvngx_dlssg.dll");
        Junk(exe, "dlss5-autopilot.json", /*lang=json,strict*/ "{\"files\": [\"nvngx_dlssg.dll\"]}" + new string(' ', 1024 * 1024));

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk);
    }

    [TestMethod]
    public async Task Subnautica32_beside_the_64_bit_builds_runtimes_is_not_dx12()
    {
        string exe = Game("Subnautica32.exe", Bits32);
        _ = Game("Subnautica.exe");
        Dll(exe, "nvngx_dlssg.dll");
        Dll(exe, Path.Join("D3D12", "D3D12Core.dll"));

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None, "graphics DLL loaded at runtime");
    }

    [TestMethod]
    public async Task Subnautica_64_bit_beside_its_own_runtimes_is_dx12()
    {
        string exe = Game("Subnautica.exe");
        Dll(exe, "nvngx_dlssg.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk, "no graphics DLL imported statically", "nvngx_dlssg.dll");
    }

    [TestMethod]
    public async Task A_32_bit_game_with_a_32_bit_agility_sdk_is_dx12()
    {
        string exe = Game(machine: Bits32);
        Dll(exe, Path.Join("D3D12", "D3D12Core.dll"), Bits32);

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk);
    }

    [TestMethod]
    public async Task A_file_a_32_bit_game_cannot_read_is_no_evidence()
    {
        string exe = Game(machine: Bits32);
        Junk(exe, "nvngx_dlssg.dll");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task Arkham_knight_with_a_swappers_dlss_files_is_dx11()
    {
        string exe = Game("BatmanAK.exe", imports: "d3d11.dll");
        Dll(exe, "nvngx_dlss.dll");
        Dll(exe, "nvngx_dlssg.dll");
        Junk(exe, "DLSS-Swapper.addon64");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_swappers_dlss_beside_a_d3d9_game_says_nothing_about_it()
    {
        string exe = Game(imports: "d3d9.dll");
        Dll(exe, "nvngx_dlss.dll");
        Junk(exe, "ngx-swapper.addon32");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task An_agility_sdk_still_counts_beside_a_foreign_dlss_tool()
    {
        string exe = Game(imports: "d3d11.dll");
        Dll(exe, Path.Join("D3D12", "D3D12Core.dll"));
        Junk(exe, "fakenvapi.ini");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk, "D3D12Core.dll");
    }

    [TestMethod]
    [DataRow("dlss5-feed.addon64")]
    [DataRow("renodx-hdr.addon64")]
    public async Task Our_add_ons_and_add_ons_that_do_not_say_dlss_are_not_another_tool(string addon)
    {
        string exe = Game(imports: "d3d9.dll");
        Dll(exe, "nvngx_dlss.dll");
        Junk(exe, addon);

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.DlssFiles);
    }

    [TestMethod]
    public async Task What_our_optiscaler_route_wrote_is_not_another_tool()
    {
        string exe = Game(imports: "d3d9.dll");
        Dll(exe, "nvngx_dlss.dll");
        Junk(exe, "OptiScaler.ini", "[x]");
        Junk(exe, "nvngx.dll_dlssnr.dll");
        Record(exe, "OptiScaler.ini", "nvngx.dll_dlssnr.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.DlssFiles);
    }

    [TestMethod]
    public async Task Atomic_heart_importing_d3d11_with_frame_generation_in_its_unreal_plugin_is_dx12()
    {
        Folder(Work, "Engine");
        string exe = Game(Path.Join("AtomicHeart", "Binaries", "Win64", "AtomicHeart-Win64-Shipping.exe"), imports: "d3d11.dll");
        Write(new TestPe(), Path.Join("Engine", "Plugins", "Runtime", "Nvidia", "StreamlineDLSSG", "Binaries", "ThirdParty", "Win64", "nvngx_dlssg.dll"));

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.UnrealPlugin, "StreamlineDLSSG", "nvngx_dlssg.dll");
    }

    [TestMethod]
    public async Task Frame_generation_in_an_unreal_plugin_outside_an_unreal_layout_is_not_read()
    {
        string exe = Game(Path.Join("AtomicHeart", "Binaries", "Win64", "AtomicHeart.exe"), imports: "d3d11.dll");
        Write(new TestPe(), Path.Join("Engine", "Plugins", "Runtime", "Nvidia", "DLSS", "Binaries", "ThirdParty", "Win64", "nvngx_dlssg.dll"));
        Directory.Delete(In("Engine"), recursive: true);

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_protected_executable_with_frame_generation_beside_it_is_dx12()
    {
        string exe = Game();
        Dll(exe, "nvngx_dlssg.dll");

        ApiDecision decision = await GraphicsApiDecision.DecideAsync(exe, new ProtectedExecutable(), Search, Ct);

        Is(decision, GraphicsApi.DX12, ApiReason.AgilitySdk);
    }

    [TestMethod]
    public async Task A_protected_executable_with_nothing_beside_it_is_unknown()
    {
        string exe = Game();

        ApiDecision decision = await GraphicsApiDecision.DecideAsync(exe, new ProtectedExecutable(), Search, Ct);

        Is(decision, GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task An_unreal_d3d11_game_with_no_frame_generation_plugin_is_dx11()
    {
        Folder(Work, "Engine");
        string exe = Game(Path.Join("Game", "Binaries", "Win64", "Game.exe"), imports: "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.StaticImport);
    }
}
