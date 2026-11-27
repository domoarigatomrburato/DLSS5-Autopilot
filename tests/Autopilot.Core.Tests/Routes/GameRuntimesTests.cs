using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.Install;

namespace Autopilot.Core.Tests.Routes;

/// <summary>What a game ships of its own, read from the names in its folders: DLSS, FSR or XeSS, a Remix runtime.</summary>
[TestClass]
public sealed class GameRuntimesTests(TestContext testContext) : ExecutableTestBase(testContext)
{
    private const string Exe = "Binaries/Win64/Game.exe";

    [TestMethod]
    [DataRow("sl.interposer.dll")]
    [DataRow("sl.dlss.dll")]
    [DataRow("sl.common.dll")]
    [DataRow("sl.dlss_g.dll")]
    [DataRow("sl.reflex.dll")]
    public async Task A_streamline_file_beside_the_exe_is_own_dlss(string name)
    {
        Ship(name);

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsTrue(runtimes.OwnDlss);
        CollectionAssert.AreEqual(new[] { Rel(name) }, runtimes.DlssEvidence.ToList());
    }

    [TestMethod]
    [DataRow("nvngx_dlss.dlsss")]
    [DataRow("nvngx_dlss.dll.bak")]
    [DataRow("_nvngx.dll")]
    public async Task A_runtime_renamed_to_switch_it_off_is_own_dlss(string name)
    {
        Ship(name);

        Assert.IsTrue((await ReadAsync()).OwnDlss);
    }

    [TestMethod]
    public async Task An_nvngx_dlss_the_game_shipped_is_own_dlss()
    {
        Ship("nvngx_dlss.dll");

        Assert.IsTrue((await ReadAsync()).OwnDlss);
    }

    [TestMethod]
    public async Task An_nvngx_dlss_on_this_tools_record_is_not_own_dlss()
    {
        Ship("nvngx_dlss.dll");

        GameRuntimes runtimes = await ReadAsync(Recorded.Ours(Rel("nvngx_dlss.dll")));

        Assert.IsFalse(runtimes.OwnDlss);
        Assert.IsEmpty(runtimes.DlssEvidence);
    }

    [TestMethod]
    public async Task A_recorded_path_matches_with_either_slash_and_any_case()
    {
        Ship("ngx/nvngx_dlss.dll");

        Assert.IsFalse((await ReadAsync(Recorded.Ours("BINARIES\\Win64\\NGX\\NVNGX_DLSS.DLL"))).OwnDlss);
    }

    [TestMethod]
    public async Task A_swapped_nvngx_dlss_with_the_games_backup_beside_it_is_own_dlss()
    {
        Ship("nvngx_dlss.dll");
        Ship("nvngx_dlss.dll.invidia-claritatis-backup");

        GameRuntimes runtimes = await ReadAsync(Recorded.Swapped(Rel("nvngx_dlss.dll")));

        Assert.IsTrue(runtimes.OwnDlss);
        CollectionAssert.AreEqual(new[] { Rel("nvngx_dlss.dll") }, runtimes.DlssEvidence.ToList());
    }

    [TestMethod]
    public async Task Nested_unreal_dlss_under_engine_plugins_is_own_dlss()
    {
        string nested = "Engine/Plugins/Runtime/Nvidia/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll";
        Plant(nested);
        Plant("Engine/Plugins/Runtime/Nvidia/DLSS/Binaries/ThirdParty/Win64/nvngx_dlssd.dll");
        Executable();

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsTrue(runtimes.OwnDlss);
        Assert.IsTrue(runtimes.ShipsRayReconstruction);
        CollectionAssert.Contains(runtimes.DlssEvidence.ToList(), Path.Join("Engine", "Plugins", "Runtime", "Nvidia", "DLSS", "Binaries", "ThirdParty", "Win64", "nvngx_dlss.dll"));
    }

    [TestMethod]
    public async Task A_nested_runtime_on_the_record_is_not_own_dlss()
    {
        Plant("Engine/Plugins/nvngx_dlss.dll");
        Executable();

        Assert.IsFalse((await ReadAsync(Recorded.Ours("Engine/Plugins/nvngx_dlss.dll"))).OwnDlss);
    }

    [TestMethod]
    public async Task Dlss_inside_an_asset_folder_is_not_found()
    {
        Plant("Content/Movies/nvngx_dlss.dll");
        Executable();

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsFalse(runtimes.OwnDlss);
        Assert.IsEmpty(runtimes.DlssEvidence);
    }

    [TestMethod]
    public async Task Streamline_in_data_streamline_is_found_as_nba_2k27_keeps_it()
    {
        Plant("data/streamline/sl.interposer.dll");
        Executable();

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsTrue(runtimes.OwnDlss);
        CollectionAssert.AreEqual(new[] { Path.Join("data", "streamline", "sl.interposer.dll") }, runtimes.DlssEvidence.ToList());
    }

    [TestMethod]
    public async Task Dlss_inside_a_dot_folder_is_not_found()
    {
        Plant(".backup/nvngx_dlss.dll");
        Executable();

        Assert.IsFalse((await ReadAsync()).OwnDlss);
    }

    [TestMethod]
    public async Task Dlss_behind_a_link_is_not_found()
    {
        Executable();
        string elsewhere = In("elsewhere");
        TestFiles.Plant(Path.Join(elsewhere, "nvngx_dlss.dll"), "MZ");
        Link(In("Game", "Linked"), elsewhere);

        Assert.IsFalse((await ReadAsync()).OwnDlss);
    }

    [TestMethod]
    public async Task Frame_generation_alone_is_evidence_but_not_own_dlss()
    {
        Ship("nvngx_dlssg.dll");

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsFalse(runtimes.OwnDlss);
        Assert.IsTrue(runtimes.ShipsFrameGeneration);
        Assert.IsFalse(runtimes.ShipsRayReconstruction);
        CollectionAssert.AreEqual(new[] { Rel("nvngx_dlssg.dll") }, runtimes.DlssEvidence.ToList());
    }

    [TestMethod]
    public async Task Ray_reconstruction_alone_is_evidence_but_not_own_dlss()
    {
        Ship("nvngx_dlssd.dll");

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsFalse(runtimes.OwnDlss);
        Assert.IsTrue(runtimes.ShipsRayReconstruction);
    }

    [TestMethod]
    public async Task Streamline_frame_generation_is_the_games_own_frame_generation()
    {
        Ship("sl.dlss_g.dll");

        Assert.IsTrue((await ReadAsync()).ShipsFrameGeneration);
    }

    [TestMethod]
    public async Task Frame_generation_this_tool_added_is_not_the_games()
    {
        Ship("nvngx_dlss.dll");
        Ship("nvngx_dlssg.dll");

        GameRuntimes runtimes = await ReadAsync(Recorded.Ours(Rel("nvngx_dlssg.dll")));

        Assert.IsTrue(runtimes.OwnDlss);
        Assert.IsFalse(runtimes.ShipsFrameGeneration);
    }

    [TestMethod]
    [DataRow("ffx_fsr2_api_x64.dll", Upscaler.Fsr)]
    [DataRow("ffx_fsr3upscaler_x64.dll", Upscaler.Fsr)]
    [DataRow("amd_fidelityfx_dx12.dll", Upscaler.Fsr)]
    [DataRow("libxess.dll", Upscaler.Xess)]
    [DataRow("libxess_dx11.dll", Upscaler.Xess)]
    public async Task An_upscaler_runtime_and_no_dlss_is_that_upscaler(string name, Upscaler kind)
    {
        Ship(name);

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsFalse(runtimes.OwnDlss);
        Assert.AreEqual(kind, runtimes.Upscaler);
        CollectionAssert.AreEqual(new[] { Rel(name) }, runtimes.UpscalerEvidence.ToList());
    }

    [TestMethod]
    public async Task Fsr_and_xess_together_count_as_fsr()
    {
        Ship("libxess.dll");
        Ship("ffx_fsr3_x64.dll");

        Assert.AreEqual(Upscaler.Fsr, (await ReadAsync()).Upscaler);
    }

    [TestMethod]
    public async Task A_nested_upscaler_runtime_is_found_by_the_walk()
    {
        Plant("Engine/Binaries/ThirdParty/XeSS/libxess.dll");
        Executable();

        GameRuntimes runtimes = await ReadAsync();

        Assert.AreEqual(Upscaler.Xess, runtimes.Upscaler);
        CollectionAssert.AreEqual(new[] { Path.Join("Engine", "Binaries", "ThirdParty", "XeSS", "libxess.dll") }, runtimes.UpscalerEvidence.ToList());
    }

    [TestMethod]
    public async Task Optiscalers_bundled_upscalers_are_not_the_games()
    {
        Plant("OptiScaler/libxess.dll");
        Plant("Licenses/amd_fidelityfx_dx12.dll");
        Executable();

        GameRuntimes runtimes = await ReadAsync();

        Assert.AreEqual(Upscaler.None, runtimes.Upscaler);
        Assert.IsEmpty(runtimes.UpscalerEvidence);
    }

    [TestMethod]
    public async Task An_upscaler_beside_own_dlss_is_not_reported()
    {
        Ship("nvngx_dlss.dll");
        Ship("ffx_fsr2_api_x64.dll");
        Ship("libxess.dll");

        GameRuntimes runtimes = await ReadAsync();

        Assert.IsTrue(runtimes.OwnDlss);
        Assert.AreEqual(Upscaler.None, runtimes.Upscaler);
        Assert.IsEmpty(runtimes.UpscalerEvidence);
    }

    [TestMethod]
    public async Task An_upscaler_on_this_tools_record_is_not_the_games()
    {
        Ship("libxess.dll");

        Assert.AreEqual(Upscaler.None, (await ReadAsync(Recorded.Ours(Rel("libxess.dll")))).Upscaler);
    }

    [TestMethod]
    public async Task A_game_with_nothing_ships_nothing()
    {
        Executable();

        Assert.AreEqual(new GameRuntimes(false, [], Upscaler.None, [], false, false, null), await ReadAsync() with { DlssEvidence = [], UpscalerEvidence = [] });
    }

    [TestMethod]
    public async Task Files_in_the_install_folder_are_read_as_beside_the_exe()
    {
        Executable();
        string install = In("Installed");
        TestFiles.Plant(Path.Join(install, "nvngx_dlss.dll"), "MZ");

        Assert.IsTrue((await GameRuntimesReader.ReadAsync(In("Game", Exe), In("Game"), install, null, Ct)).OwnDlss);
    }

    [TestMethod]
    [DataRow(".trex")]
    [DataRow("bin/.trex")]
    [DataRow("bin/x64/.trex")]
    public async Task A_trex_holding_d3d9_up_to_three_levels_deep_is_the_remix_runtime(string trex)
    {
        Plant(trex + "/d3d9.dll");
        Executable();

        Assert.AreEqual(In(["Game", .. trex.Split('/')]), (await ReadAsync()).RemixRuntime);
    }

    [TestMethod]
    public async Task A_trex_four_levels_deep_is_not_found()
    {
        Plant("a/b/c/.trex/d3d9.dll");
        Executable();

        Assert.IsNull((await ReadAsync()).RemixRuntime);
    }

    [TestMethod]
    public async Task An_empty_trex_is_not_a_remix_install()
    {
        _ = Directory.CreateDirectory(In("Game", ".trex"));
        Plant(".trex/rtx.conf");
        Executable();

        Assert.IsNull((await ReadAsync()).RemixRuntime);
    }

    [TestMethod]
    [DataRow("mods/.trex")]
    [DataRow("rtx-remix/.trex")]
    [DataRow(".hidden/.trex")]
    public async Task A_trex_inside_a_skipped_folder_is_not_found(string trex)
    {
        Plant(trex + "/d3d9.dll");
        Executable();

        Assert.IsNull((await ReadAsync()).RemixRuntime);
    }

    [TestMethod]
    public async Task A_trex_behind_a_link_is_not_found()
    {
        Executable();
        string elsewhere = In("elsewhere");
        TestFiles.Plant(Path.Join(elsewhere, ".trex", "d3d9.dll"), "MZ");
        Link(In("Game", "Linked"), elsewhere);

        Assert.IsNull((await ReadAsync()).RemixRuntime);
    }

    [TestMethod]
    public async Task A_trex_beside_the_exe_is_found_before_one_in_the_game_folder()
    {
        Ship(".trex/d3d9.dll");
        Plant(".trex/d3d9.dll");

        Assert.AreEqual(In(["Game", .. Exe.Split('/')[..^1], ".trex"]), (await ReadAsync()).RemixRuntime);
    }

    [TestMethod]
    public async Task A_cancelled_read_throws()
    {
        Ship("nvngx_dlss.dll");

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => GameRuntimesReader.ReadAsync(In("Game", Exe), In("Game"), null, null, new CancellationToken(canceled: true)));
    }

    private static string Rel(string besideExe) => Path.Join([.. Exe.Split('/')[..^1], .. besideExe.Split('/')]);

    private static void Link(string link, string target)
    {
        try
        {
            _ = Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            Assert.Inconclusive($"cannot create a symbolic link here: {e.Message}");
        }
    }

    // The entries, relative to the game folder, go in a record in the executable's folder, read back through install's reader.
    private async Task<GameRuntimes> ReadAsync(params RecordEntry[] recorded)
    {
        string install = Path.GetDirectoryName(In("Game", Exe))!;
        RecordedInstall? record = null;
        if (recorded.Length > 0)
        {
            Recorded.Plant(install, Recorded.Install("Binaries/Win64", recorded));
            record = await Recorded.FindAsync(install, Ct);
        }

        return await GameRuntimesReader.ReadAsync(In("Game", Exe), In("Game"), null, record, Ct);
    }

    private void Executable() => Plant(Exe);

    // A file beside the executable.
    private void Ship(string name)
    {
        Executable();
        Plant(Rel(name));
    }

    private void Plant(string relative) => TestFiles.Plant(In(["Game", .. relative.Split('/', '\\')]), "MZ");
}
