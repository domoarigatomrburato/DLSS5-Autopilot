using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Install.Plans;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install.Plans;

[TestClass]
public sealed class ReShadeBaseTests(TestContext testContext)
{
    private const string CleanUp = "Taking other add-ons and ReShade copies out of the way";
    private const string Compiler = "Moving the game's older Direct3D compiler aside";
    private const string Dlss = "Placing nvngx_dlss.dll";
    private const string Dlssd = "Swapping nvngx_dlssd.dll";

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_fresh_Direct3D_12_game_gets_ReShade_as_dxgi_its_settings_and_the_runtimes_beside_the_executable()
    {
        using PlanBed bed = new();

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual(
            """
            Placing ReShade: place dxgi.dll
            Writing ReShade.ini: place ReShade.ini
            Placing nvngx_dlssnr.dll: place nvngx_dlssnr.dll
            Placing nvngx_dlss.dll: place nvngx_dlss.dll
            Placing the renodx-dlss5 add-on: place renodx-dlss5.addon64
            """.ReplaceLineEndings("\n"),
            plan.Lines());
        CollectionAssert.AreEqual(CachedComponents.Bytes("reshade", "ReShade64.dll"), plan.Placed("dxgi.dll"));
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX12, "dxgi.dll")]
    [DataRow(GraphicsApi.OpenGL, "opengl32.dll")]
    [DataRow(GraphicsApi.DX11, "dxgi.dll")]
    public async Task ReShade_takes_the_name_its_api_loads(GraphicsApi api, string proxy)
    {
        // The box left off: a Direct3D 11 game may go through DXVK on native.
        using PlanBed bed = new() { Facts = RouteFactsBeds.Game(api, executable: "Game.exe"), Settings = RouteSettings.Default with { Dxvk = false } };

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual($"place {proxy}", plan.Group("Placing ReShade"));
    }

    [TestMethod]
    public void A_Direct3D_9_game_off_DXVK_loads_ReShade_as_d3d9_and_through_DXVK_as_no_proxy()
    {
        Assert.AreEqual("d3d9.dll", ReShadeBase.ProxyName(RouteId.Renodx, RouteFactsBeds.Game(GraphicsApi.DX9), RouteSettings.Default));
        Assert.IsNull(ReShadeBase.ProxyName(RouteId.Native, RouteFactsBeds.Game(GraphicsApi.DX9), RouteSettings.Default));
    }

    [TestMethod]
    public async Task The_persons_name_for_ReShade_wins_over_the_apis()
    {
        using PlanBed bed = new() { Settings = RouteSettings.Default with { ReShadeLoadsAs = ReShadeProxy.D3d11 } };

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual("place d3d11.dll", plan.Group("Placing ReShade"));
    }

    [TestMethod]
    public async Task A_Vulkan_game_gets_ReShades_layer_instead_of_a_proxy()
    {
        using PlanBed bed = new() { Facts = RouteFactsBeds.Game(GraphicsApi.Vulkan, executable: "Game.exe") };

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual("layer VulkanLayer64", plan.Group("Registering ReShade's Vulkan layer"));
        Assert.IsEmpty(plan.Group("Placing ReShade"));
        CollectionAssert.AreEqual(CachedComponents.Bytes("reshade", "ReShade64.json"), plan.Manifest());
    }

    [TestMethod]
    public async Task Another_ReShade_other_routes_add_ons_and_the_older_bridge_add_on_are_moved_aside_with_a_backup()
    {
        using PlanBed bed = new([
            .. GameFolders.SecondReShade,
            TreeFile.Bytes("dlss5-feed.addon64", GameFolders.Dll("the feeder")),
            TreeFile.Bytes("dlss5-dx11-bridge.addon64", GameFolders.Dll("the old bridge")),
            TreeFile.Bytes("d3d12.dll", GameFolders.Dll("the game's own d3d12")),
        ]);

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual("aside d3d11.dll (Backup), aside dlss5-feed.addon64 (Backup), aside dlss5-dx11-bridge.addon64 (Backup)",
            plan.Group(CleanUp));
    }

    [TestMethod]
    public async Task A_hand_installed_OptiScaler_under_the_proxy_name_goes_aside_with_its_settings_and_a_note()
    {
        using PlanBed bed = new(GameFolders.HandInstalledOptiScaler);

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual("aside OptiScaler.ini (Backup)", plan.Group(CleanUp));
        Assert.AreEqual("place dxgi.dll", plan.Group("Placing ReShade"));
        CollectionAssert.Contains(plan.Header.Notes.ToList(), "an OptiScaler installed by hand as dxgi.dll was backed up and moved aside with its settings: two injectors cannot share the name");
    }

    [TestMethod]
    public async Task An_OptiScaler_that_also_names_ReShade_is_still_taken_for_OptiScaler()
    {
        using PlanBed bed = new(TreeFile.Bytes("dxgi.dll", Markers.OptiScalerNamingReShade));

        InstallPlan plan = await bed.PlanAsync(Ct);

        CollectionAssert.Contains(plan.Header.Notes.ToList(), "an OptiScaler installed by hand as dxgi.dll was backed up and moved aside with its settings: two injectors cannot share the name");
    }

    [TestMethod]
    public void Each_routes_own_add_ons_are_the_purge_tables_row()
    {
        Assert.AreEqual("dlss5-bridge.addon64", string.Join(", ", PlanNames.AddOnsOf(RouteId.Bridge)));
        Assert.IsEmpty(PlanNames.AddOnsOf(RouteId.Native));
    }

    [TestMethod]
    public async Task The_games_compiler_goes_aside_only_when_older_than_Windows_with_both_versions_in_the_notes()
    {
        using PlanBed older = new PlanBed(TreeFile.Bytes("d3dcompiler_47.dll", PlanRuns.Compiler("10.0.17763.1"))).WindowsCompiler("10.0.26100.1");
        using PlanBed newer = new PlanBed(TreeFile.Bytes("d3dcompiler_47.dll", PlanRuns.Compiler("10.0.26100.1"))).WindowsCompiler("10.0.26100.1");

        InstallPlan aside = await older.PlanAsync(Ct);
        InstallPlan kept = await newer.PlanAsync(Ct);

        Assert.AreEqual("aside d3dcompiler_47.dll (Dlss5Off)", aside.Group(Compiler));
        CollectionAssert.Contains(aside.Header.Notes.ToList(), "the game's d3dcompiler_47.dll (10.0.17763.1) is older than Windows' (10.0.26100.1): moved aside so Windows' loads");
        Assert.IsEmpty(kept.Group(Compiler));
        CollectionAssert.Contains(kept.Header.Notes.ToList(), "the game's d3dcompiler_47.dll (10.0.26100.1) is not older than Windows' (10.0.26100.1): left as it is");
    }

    [TestMethod]
    public async Task ReShade_ini_gains_the_add_on_keys_and_keeps_every_other_line_and_its_line_endings()
    {
        using PlanBed bed = new(TreeFile.Text("ReShade.ini", "[GENERAL]\nEffectSearchPaths=.\\my-shaders\n\n[RenoDX.DLSS5]\nNeuralUplift=0\n"))
        {
            Settings = RouteSettings.Default with { OverlayKey = 0x24 },
        };

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual(
            "[GENERAL]\nEffectSearchPaths=.\\my-shaders\n\n[RenoDX.DLSS5]\nNeuralUplift=0\n\n[ADDON]\nAddonPath=.\\\n\n[INPUT]\nKeyOverlay=36,0,0,0\n",
            plan.Text("ReShade.ini"));
    }

    [TestMethod]
    public async Task A_new_ReShade_ini_is_written_with_Windows_line_endings_and_the_add_on_switched_on()
    {
        using PlanBed bed = new();

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual("[ADDON]\r\nAddonPath=.\\\r\n\r\n[RenoDX.DLSS5]\r\nNeuralUplift=1\r\n", plan.Text("ReShade.ini"));
    }

    [TestMethod]
    public async Task The_games_own_nvngx_dlss_is_kept_when_asked_and_swapped_otherwise()
    {
        using PlanBed keep = new(TreeFile.Bytes("nvngx_dlss.dll", GameFolders.Dll("the game's dlss")));
        using PlanBed swap = new(TreeFile.Bytes("nvngx_dlss.dll", GameFolders.Dll("the game's dlss"))) { Settings = RouteSettings.Default with { KeepGameDlss = false } };

        InstallPlan kept = await keep.PlanAsync(Ct);
        InstallPlan swapped = await swap.PlanAsync(Ct);

        Assert.IsEmpty(kept.Group(Dlss));
        CollectionAssert.Contains(kept.Header.Skipped.ToList(), "nvngx_dlss.dll: the game's own is kept, as 'keep the game's own' asks");
        Assert.AreEqual("place nvngx_dlss.dll", swapped.Group(Dlss));
    }

    [TestMethod]
    public async Task Swapping_reaches_the_games_nested_copy_when_none_is_beside_the_executable()
    {
        using PlanBed bed = new(TreeFile.Bytes(@"Plugins\DLSS\Binaries\ThirdParty\Win64\nvngx_dlss.dll", GameFolders.Dll("the plugin's dlss")))
        {
            Settings = RouteSettings.Default with { KeepGameDlss = false },
        };

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.AreEqual("place nvngx_dlss.dll, place Plugins/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll", plan.Group(Dlss));
    }

    [TestMethod]
    public async Task Swapping_reaches_every_copy_the_game_ships_beside_and_nested()
    {
        const string nested = "Engine/Plugins/DLSS/Binaries/ThirdParty/Win64/";
        using PlanBed dlss = new(TreeFile.Bytes("nvngx_dlss.dll", GameFolders.Dll("the game's dlss")), TreeFile.Bytes(nested + "nvngx_dlss.dll", GameFolders.Dll("the plugin's dlss")))
        {
            Settings = RouteSettings.Default with { KeepGameDlss = false },
        };
        using PlanBed dlssd = new(TreeFile.Bytes("nvngx_dlssd.dll", GameFolders.Dll("the game's dlssd")), TreeFile.Bytes(nested + "nvngx_dlssd.dll", GameFolders.Dll("the plugin's dlssd")))
        {
            Settings = RouteSettings.Default with { RayReconstructionLabel = "1.0" },
        };

        Assert.AreEqual($"place nvngx_dlss.dll, place {nested}nvngx_dlss.dll", (await dlss.PlanAsync(Ct)).Group(Dlss));
        Assert.AreEqual($"place nvngx_dlssd.dll, place {nested}nvngx_dlssd.dll", (await dlssd.PlanAsync(Ct)).Group(Dlssd));
    }

    [TestMethod]
    public async Task Ray_reconstruction_is_swapped_only_when_chosen_and_the_game_ships_it()
    {
        RouteSettings chosen = RouteSettings.Default with { RayReconstructionLabel = "1.0" };
        using PlanBed ships = new(TreeFile.Bytes("nvngx_dlssd.dll", GameFolders.Dll("the game's dlssd"))) { Settings = chosen };
        using PlanBed notChosen = new(TreeFile.Bytes("nvngx_dlssd.dll", GameFolders.Dll("the game's dlssd")));
        using PlanBed absent = new() { Settings = chosen };

        Assert.AreEqual("place nvngx_dlssd.dll", (await ships.PlanAsync(Ct)).Group(Dlssd));
        Assert.IsEmpty((await notChosen.PlanAsync(Ct)).Group(Dlssd));
        Assert.IsEmpty((await absent.PlanAsync(Ct)).Group(Dlssd));
    }

    [TestMethod]
    public async Task A_ray_reconstruction_download_that_failed_places_nothing_and_says_so()
    {
        using PlanBed bed = new(TreeFile.Bytes("nvngx_dlssd.dll", GameFolders.Dll("the game's dlssd"))) { Settings = RouteSettings.Default with { RayReconstructionLabel = "1.0" } };
        bed.Files.Fail("dlssd", "no source served it");

        InstallPlan plan = await bed.PlanAsync(Ct);

        Assert.IsEmpty(plan.Group(Dlssd));
        Assert.AreEqual("nvngx_dlssd.dll was not swapped: the download did not arrive (no source served it). The game keeps the one it shipped, and everything else installed normally.", plan.Warnings());
    }

    [TestMethod]
    public async Task A_ray_reconstruction_download_that_failed_keeps_the_previous_swap()
    {
        using PlanBed bed = await PlanFolders.SwappedDlssdAsync(Ct);
        bed.Files.Fail("dlssd", "no source served it");

        InstallPlan again = await bed.PlanAsync(Ct);

        Assert.AreEqual("keep nvngx_dlssd.dll", again.Group(Dlssd));
        Assert.IsInstanceOfType<InstallCommitted>(await bed.Bed.InstallAsync(again, Ct));
        Assert.IsTrue(bed.Bed.Holds("nvngx_dlssd.dll", CachedComponents.Bytes("dlssd", "nvngx_dlssd.dll")));
    }

    [TestMethod]
    public async Task A_kept_swap_says_so_and_records_the_previous_installs_version()
    {
        using PlanBed bed = await PlanFolders.SwappedDlssdAsync(Ct);
        bed.Files.Fail("dlssd", "no source served it");
        bed.Files.Version("dlssd", "2.0");

        InstallPlan again = await bed.PlanAsync(Ct);

        Assert.AreEqual("1.0", again.Header.Components["dlssd"]);
        Assert.AreEqual("nvngx_dlssd.dll could not be downloaded (no source served it): the one the previous install swapped in stays.", again.Warnings());
    }

    [TestMethod]
    public async Task Our_previous_files_are_left_to_the_transaction_and_the_compiler_moved_aside_stays_aside()
    {
        using PlanBed bed = new PlanBed(TreeFile.Bytes("d3dcompiler_47.dll", PlanRuns.Compiler("10.0.17763.1"))).WindowsCompiler("10.0.26100.1");
        Assert.IsInstanceOfType<InstallCommitted>(await bed.InstallAsync(Ct));

        InstallPlan again = await bed.PlanAsync(Ct);

        Assert.AreEqual("aside d3dcompiler_47.dll (Dlss5Off)", again.Group(Compiler));
        Assert.IsEmpty(again.Group(CleanUp));
        Assert.IsInstanceOfType<InstallCommitted>(await bed.Bed.InstallAsync(again, Ct));
    }
}
