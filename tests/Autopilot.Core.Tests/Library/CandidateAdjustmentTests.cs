using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Library;

/// <summary>One test per row of the adjustments table in the library spec, and the launcher names behind it.</summary>
[TestClass]
public sealed class CandidateAdjustmentTests(TestContext testContext) : LibraryTestBase(testContext)
{
    private const long Mb = GameFolder.Megabyte;

    [TestMethod]
    public async Task A_trial_ranks_below_the_full_game_beside_it_but_stays_in_the_list()
    {
        using GameFolder game = new("Need for Speed Heat");
        _ = game.Exe("NeedForSpeedHeat.exe", 1000).Exe("NeedForSpeedHeatTrial.exe", 400_000)
            .Exe("NFS16.exe", 1000).Exe("NFS16_trial.exe", 400_000);

        AssertOrder(await game.RankedAsync(Ct), "NeedForSpeedHeat.exe", "NFS16.exe", "NeedForSpeedHeatTrial.exe", "NFS16_trial.exe");
    }

    [TestMethod]
    [DataRow("Zq9Arena Demo.exe")]
    [DataRow("Zq9Arena.demo.exe")]
    [DataRow("Zq9Arena-Trial.exe")]
    [DataRow("Zq9Arena_demo.exe")]
    [DataRow("Zq9ArenaTrial.exe")]
    public async Task Each_way_of_spelling_a_trial_or_demo_suffix_is_recognised(string name)
    {
        using GameFolder game = new();
        _ = game.Exe("Zq9Arena.exe", 1000).Exe(name, 400_000);

        AssertOrder(await game.RankedAsync(Ct), "Zq9Arena.exe", name);
    }

    [TestMethod]
    public async Task A_demo_only_install_keeps_its_executable()
    {
        using GameFolder game = new("Some Game");
        _ = game.Exe("SomeGameDemo.exe", 1000);

        AssertOrder(await game.RankedAsync(Ct), "SomeGameDemo.exe");
    }

    [TestMethod]
    public async Task A_trial_with_no_full_executable_of_its_name_keeps_its_place()
    {
        using GameFolder game = new();
        _ = game.Exe("kilo.exe").Exe("Zq9ArenaTrial.exe");

        AssertOrder(await game.RankedAsync(Ct), "Zq9ArenaTrial.exe", "kilo.exe");
    }

    [TestMethod]
    public async Task A_full_executable_in_another_folder_does_not_demote_the_trial()
    {
        using GameFolder game = new();
        _ = game.Exe("a/Zq9Arena.exe").Exe("b/Zq9ArenaTrial.exe").Exe("c/kilo.exe");

        AssertOrder(await game.RankedAsync(Ct), "a/Zq9Arena.exe", "b/Zq9ArenaTrial.exe", "c/kilo.exe");
    }

    [TestMethod]
    public async Task A_launcher_beside_the_game_it_starts_is_not_offered()
    {
        using GameFolder game = new("UBOAT");
        _ = game.Exe("UBOAT.exe", 100_000).UnityData("UBOAT.exe").Exe("UBOAT Launcher.exe", (8 * Mb) + 100_000);

        AssertOrder(await game.RankedAsync(Ct), "UBOAT.exe");
    }

    [TestMethod]
    public async Task A_launcher_whose_game_is_not_named_after_it_ranks_below_the_game_but_stays_listed()
    {
        using GameFolder game = new("Grand Theft Auto V");
        _ = game.Exe("GTA5.exe", 40_960).Exe("GTAVLauncher.exe", 20 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "GTA5.exe", "GTAVLauncher.exe");
    }

    [TestMethod]
    public async Task A_folder_holding_only_a_launcher_still_offers_it()
    {
        using GameFolder game = new();
        _ = game.Exe("a/b/c/Launcher.exe");

        AssertOrder(await game.RankedAsync(Ct), "a/b/c/Launcher.exe");
    }

    [TestMethod]
    [DataRow("GTAVLauncher")]
    [DataRow("UBOAT Launcher")]
    [DataRow("Game_Launcher")]
    [DataRow("Launcher")]
    [DataRow("Launch")]
    [DataRow("Launch Game")]
    [DataRow("launch_game")]
    [DataRow("start_protected_game")]
    [DataRow("EOSLauncher")]
    [DataRow("EpicGamesLauncher")]
    [DataRow("EAStart")]
    [DataRow("OriginLaunch")]
    [DataRow("uplay_start")]
    [DataRow("Ubisoft Launch")]
    public async Task A_launcher_name_ranks_below_any_other_however_large(string name)
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe").Exe($"{name}.exe", 20 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe", $"{name}.exe");
    }

    [TestMethod]
    [DataRow("SpaceLauncherSimulator")]
    [DataRow("Launchpad")]
    [DataRow("Relaunch")]
    [DataRow("Earth")]
    [DataRow("Epicenter")]
    [DataRow("Startup")]
    public async Task A_name_that_only_contains_part_of_a_launcher_name_is_not_one(string name)
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe").Exe($"{name}.exe", 20 * Mb);

        AssertOrder(await game.RankedAsync(Ct), $"{name}.exe", "alpha.exe");
    }

    [TestMethod]
    [DataRow("bin")]
    [DataRow("bin/x64")]
    [DataRow("bin/win64")]
    public async Task Half_Life_2_offers_its_game_before_the_tools_in_bin(string engineFolder)
    {
        using GameFolder game = SourceGame().Source(engineFolder);

        AssertOrder(await game.RankedAsync(Ct), "hl2.exe", "bin/hammer.exe", "bin/elementviewer.exe", "bin/hlmv.exe");
    }

    [TestMethod]
    public async Task The_dedicated_server_beside_a_source_game_does_not_outrank_it()
    {
        using GameFolder game = SourceGame().Source().Exe("srcds.exe", 6 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "hl2.exe", "bin/hammer.exe", "bin/elementviewer.exe", "bin/hlmv.exe");
    }

    [TestMethod]
    public async Task An_engine_dll_in_bin_without_a_game_info_file_is_not_the_source_layout()
    {
        using GameFolder game = SourceGame().Source(gameInfo: false);

        AssertOrder(await game.RankedAsync(Ct), "bin/hammer.exe", "bin/elementviewer.exe", "bin/hlmv.exe", "hl2.exe");
    }

    [TestMethod]
    public async Task A_game_info_file_without_an_engine_dll_in_bin_is_not_the_source_layout()
    {
        using GameFolder game = SourceGame().Source(engine: false);

        AssertOrder(await game.RankedAsync(Ct), "bin/hammer.exe", "bin/elementviewer.exe", "bin/hlmv.exe", "hl2.exe");
    }

    [TestMethod]
    [DataRow("Zq9Arena32", "Zq9Arena64")]
    [DataRow("Zq9Arena_x86", "Zq9Arena_x64")]
    [DataRow("Zq9Arena-win32", "Zq9Arena-win64")]
    [DataRow("Zq9Arena 32bit", "Zq9Arena 64bit")]
    [DataRow("Zq9Arena32", "Zq9Arena")]
    [DataRow("Zq9Arena", "Zq9Arena64")]
    public async Task The_32_bit_build_of_a_game_gives_way_to_its_64_bit_twin_but_stays_listed(string thirtyTwo, string sixtyFour)
    {
        using GameFolder game = new();
        _ = game.Pe($"{thirtyTwo}.exe", 6, TestPe.I386).Pe($"{sixtyFour}.exe", 1, TestPe.Amd64);

        AssertOrder(await game.RankedAsync(Ct), $"{sixtyFour}.exe", $"{thirtyTwo}.exe");
    }

    [TestMethod]
    public async Task Two_builds_of_the_same_bitness_are_not_twins()
    {
        using GameFolder game = new();
        _ = game.Pe("Zq9Arena32.exe", 6, TestPe.Amd64).Pe("Zq9Arena.exe", 1, TestPe.Amd64);

        AssertOrder(await game.RankedAsync(Ct), "Zq9Arena32.exe", "Zq9Arena.exe");
    }

    [TestMethod]
    public async Task A_twin_whose_header_cannot_be_read_is_left_alone()
    {
        using GameFolder game = new();
        _ = game.Exe("Zq9Arena32.exe", 6 * Mb).Pe("Zq9Arena.exe", 1, TestPe.Amd64);

        AssertOrder(await game.RankedAsync(Ct), "Zq9Arena32.exe", "Zq9Arena.exe");
    }

    [TestMethod]
    public async Task A_twin_that_windows_refuses_to_open_is_left_alone()
    {
        using GameFolder game = new();
        _ = game.Pe("Zq9Arena32.exe", 6, TestPe.I386).Pe("Zq9Arena.exe", 1, TestPe.Amd64).Deny("Zq9Arena32.exe");

        AssertOrder(await game.RankedAsync(Ct), "Zq9Arena32.exe", "Zq9Arena.exe");
    }

    [TestMethod]
    public async Task Builds_in_different_folders_are_not_twins()
    {
        using GameFolder game = new();
        _ = game.Pe("a/Zq9Arena.exe", 1, TestPe.Amd64).Pe("b/Zq9Arena32.exe", 6, TestPe.I386);

        AssertOrder(await game.RankedAsync(Ct), "b/Zq9Arena32.exe", "a/Zq9Arena.exe");
    }

    [TestMethod]
    public async Task A_lone_32_bit_executable_is_offered()
    {
        using GameFolder game = new();
        _ = game.Pe("Zq9Arena32.exe", 1, TestPe.I386);

        AssertOrder(await game.RankedAsync(Ct), "Zq9Arena32.exe");
    }

    private static GameFolder SourceGame()
    {
        GameFolder game = new("Half-Life 2");
        _ = game.Exe("hl2.exe", 120_000).Exe("bin/elementviewer.exe", 3 * Mb).Exe("bin/hlmv.exe", 2 * Mb).Exe("bin/hammer.exe", 4 * Mb);
        return game;
    }
}
