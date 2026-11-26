namespace Autopilot.Core.Tests.Library;

/// <summary>One test per row of the score table in the library spec.</summary>
[TestClass]
public sealed class CandidateScoreTests(TestContext testContext) : LibraryTestBase(testContext)
{
    private const long Mb = GameFolder.Megabyte;

    [TestMethod]
    public async Task An_unreal_shipping_executable_outranks_an_unrelated_one()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe").Exe("zulu-Win64-Shipping.exe");

        AssertOrder(await game.RankedAsync(Ct), "zulu-Win64-Shipping.exe", "alpha.exe");
    }

    [TestMethod]
    public async Task The_binaries_folders_rank_in_tiers_above_the_game_folder()
    {
        using GameFolder game = new();
        _ = game.Exe("other/echo.exe").Exe("delta.exe").Exe("bin/charlie.exe").Exe("Game/Binaries/Win32/bravo.exe").Exe("Binaries/Win64/alpha.exe");

        AssertOrder(
            await game.RankedAsync(Ct),
            "Binaries/Win64/alpha.exe",
            "Game/Binaries/Win32/bravo.exe",
            "bin/charlie.exe",
            "delta.exe",
            "other/echo.exe");
    }

    [TestMethod]
    [DataRow("Game/bin/Win64/alpha.exe")]
    [DataRow("Game/Binaries/Win64/alpha.exe")]
    [DataRow("Game/bin/Win32/alpha.exe")]
    [DataRow("Game/Binaries/Win32/alpha.exe")]
    [DataRow("Game/bin/alpha.exe")]
    public async Task A_binaries_folder_below_another_folder_still_beats_an_executable_in_the_game_folder(string path)
    {
        using GameFolder game = new();
        _ = game.Exe("kilo.exe").Exe(path);

        AssertOrder(await game.RankedAsync(Ct), path, "kilo.exe");
    }

    [TestMethod]
    public async Task A_folder_with_no_binaries_name_earns_nothing_for_its_depth()
    {
        using GameFolder game = new();
        _ = game.Exe("kilo.exe").Exe("Game/misc/alpha.exe");

        AssertOrder(await game.RankedAsync(Ct), "kilo.exe", "Game/misc/alpha.exe");
    }

    [TestMethod]
    [DataRow("Engine/Binaries/Win64/alpha.exe")]
    [DataRow("Snowfall/Engine/Binaries/Win64/alpha.exe")]
    public async Task Engine_tooling_is_dropped_from_the_answer(string path)
    {
        using GameFolder game = new();
        _ = game.Exe("kilo.exe").Exe(path);

        AssertOrder(await game.RankedAsync(Ct), "kilo.exe");
    }

    [TestMethod]
    [DataRow("Zq9Arena.exe")]
    [DataRow("zq9 ARENA extended.exe")]
    [DataRow("Arena.exe")]
    [DataRow("Z-Q9.exe")]
    public async Task A_name_that_contains_or_is_contained_in_the_folder_name_gains(string name)
    {
        using GameFolder game = new();
        _ = game.Exe("abc.exe").Exe(name);

        AssertOrder(await game.RankedAsync(Ct), name, "abc.exe");
    }

    // The reference's rule, kept until a corpus check says a minimum length is needed (library spec, open question 4).
    [TestMethod]
    public async Task A_one_letter_name_that_is_part_of_the_folder_name_matches_too()
    {
        using GameFolder game = new();
        _ = game.Exe("abc.exe").Exe("z.exe");

        AssertOrder(await game.RankedAsync(Ct), "z.exe", "abc.exe");
    }

    [TestMethod]
    public async Task An_executable_in_the_game_folder_beats_a_slightly_larger_one_below_it()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe").Exe("sub/bravo.exe", 50 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe", "sub/bravo.exe");
    }

    [TestMethod]
    public async Task A_larger_executable_outranks_a_smaller_one()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe", Mb).Exe("bravo.exe", 10 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "bravo.exe", "alpha.exe");
    }

    [TestMethod]
    public async Task Size_counts_for_no_more_than_300_megabytes()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe", 300 * Mb).Exe("bravo.exe", 600 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe", "bravo.exe");
    }

    [TestMethod]
    public async Task Each_path_separator_costs_a_little()
    {
        using GameFolder game = new();
        _ = game.Exe("sub/deep/alpha.exe").Exe("sub/zulu.exe");

        AssertOrder(await game.RankedAsync(Ct), "sub/zulu.exe", "sub/deep/alpha.exe");
    }

    [TestMethod]
    public async Task A_unity_player_with_its_data_folder_beats_a_larger_executable()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe").UnityData("alpha.exe").Exe("bravo.exe", 100 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe", "bravo.exe");
    }

    [TestMethod]
    [DataRow("srcds")]
    [DataRow("unins")]
    [DataRow("setup")]
    [DataRow("vcredist")]
    [DataRow("dxsetup")]
    [DataRow("dotnet")]
    [DataRow("prereq")]
    [DataRow("redist")]
    [DataRow("crashhandler")]
    [DataRow("crashreport")]
    [DataRow("crashpad")]
    [DataRow("easyanticheat")]
    [DataRow("battleye")]
    [DataRow("touchup")]
    [DataRow("installer")]
    [DataRow("activation")]
    [DataRow("cleanup")]
    [DataRow("helper")]
    [DataRow("webhelper")]
    [DataRow("unitycrashhandler")]
    [DataRow("ue4prereqsetup")]
    [DataRow("ue5prereqsetup")]
    [DataRow("epicwebhelper")]
    [DataRow("dlss5-feed-host")]
    [DataRow("digitalextras")]
    [DataRow("extrasapp")]
    [DataRow("artbook")]
    [DataRow("soundtrack")]
    public async Task A_helper_name_in_any_case_is_dropped_however_large_the_file(string part)
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe").Exe($"Kilo-{part.ToUpperInvariant()}.exe", 20 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe");
    }

    [TestMethod]
    public async Task A_folder_holding_only_helpers_still_answers()
    {
        using GameFolder game = new();
        _ = game.Exe("setup.exe").Exe("unins000.exe");

        AssertOrder(await game.RankedAsync(Ct), "setup.exe", "unins000.exe");
    }

    [TestMethod]
    public async Task Ghostwire_runs_from_its_project_folder_not_the_stub_in_the_root()
    {
        using GameFolder game = new("Ghostwire Tokyo");
        _ = game.Exe("GWT.exe", 5 * Mb).Exe("Snowfall/Binaries/Win64/GWT.exe", 200 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "Snowfall/Binaries/Win64/GWT.exe", "GWT.exe");
    }

    [TestMethod]
    public async Task Kingdom_Come_2_is_found_in_its_deep_bin_folder_and_beats_the_launcher_in_the_root()
    {
        using GameFolder game = new("Kingdom Come Deliverance II");
        _ = game.Exe("KCDLauncher.exe", 10 * Mb).Exe("Bin/Win64MasterMasterSteamPGO/KingdomCome.exe", 50 * Mb);

        AssertOrder(await game.RankedAsync(Ct), "Bin/Win64MasterMasterSteamPGO/KingdomCome.exe", "KCDLauncher.exe");
    }

    [TestMethod]
    public async Task Equal_scores_are_ordered_by_path_ignoring_case()
    {
        using GameFolder game = new();
        _ = game.Exe("Bravo.exe").Exe("alpha.exe").Exe("Charlie.exe");

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe", "Bravo.exe", "Charlie.exe");
    }

    [TestMethod]
    public async Task The_candidates_are_full_paths()
    {
        using GameFolder game = new();
        _ = game.Exe("bin/alpha.exe");

        CollectionAssert.AreEqual(new[] { game.PathOf("bin/alpha.exe") }, (await game.SearchAsync(Ct)).Candidates.ToArray());
    }
}
