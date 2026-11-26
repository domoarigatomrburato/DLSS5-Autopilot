using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class SteamSourceTests(TestContext testContext) : IDisposable
{
    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    private string SteamRoot => _bed.PathOf("Steam");

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_fully_installed_game_is_listed_under_its_store_name()
    {
        string folder = InstalledSteam().Common("Portal 2");
        Manifest(SteamRoot, 620, "Portal 2", "Portal 2", "4");

        StoreGame game = (await FindAsync()).Single();

        Assert.AreEqual(new StoreGame("Portal 2", folder, GameSource.Steam, null), game);
    }

    [TestMethod]
    [DataRow("4")]
    [DataRow("6")]
    [DataRow(null)]
    public async Task A_game_whose_manifest_says_fully_installed_or_says_nothing_is_listed_even_without_an_executable(string? flags)
    {
        _ = InstalledSteam().Common("Hades");
        Manifest(SteamRoot, 1145360, "Hades", "Hades", flags);

        Assert.AreEqual("Hades", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    [DataRow("1042")]
    [DataRow("1026")]
    [DataRow("not a number")]
    public async Task A_game_still_downloading_is_not_offered(string flags)
    {
        _ = InstalledSteam().Common("Battlefield 6");
        Manifest(SteamRoot, 2807960, "Battlefield 6", "Battlefield 6", flags);

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task A_game_whose_manifest_is_not_complete_but_holds_an_executable_is_kept()
    {
        _ = InstalledSteam().CommonGame("Updating Game");
        Manifest(SteamRoot, 10, "Updating Game", "Updating Game", "1026");

        Assert.AreEqual("Updating Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task A_folder_an_uninstall_left_behind_is_not_offered()
    {
        string leftover = InstalledSteam().Common("Old Game");
        _ = _bed.WriteFile(Path.Join(Path.GetRelativePath(_bed.Root, leftover), "saves", "slot1.save"), "x");
        _ = _bed.WriteFile(Path.Join(Path.GetRelativePath(_bed.Root, leftover), "config.ini"), "x");

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task A_folder_with_no_manifest_but_an_executable_is_kept_under_its_folder_name()
    {
        string folder = InstalledSteam().CommonGame("Lost Manifest");

        Assert.AreEqual(new StoreGame("Lost Manifest", folder, GameSource.Steam, null), (await FindAsync()).Single());
    }

    [TestMethod]
    public async Task A_folder_holding_our_install_record_is_kept_even_when_the_game_is_gone()
    {
        string folder = InstalledSteam().Common("Removed Game");
        _ = _bed.WriteFile(Path.Join(Path.GetRelativePath(_bed.Root, folder), "dlss5-autopilot.json"), "{}");

        Assert.AreEqual(folder, (await FindAsync()).Single().Folder);
    }

    [TestMethod]
    public async Task A_folder_too_full_to_check_in_one_listing_is_kept()
    {
        string folder = InstalledSteam().Common("Huge Leftover");
        for (int i = 0; i < 400; i++)
        {
            await File.WriteAllTextAsync(Path.Join(folder, $"file{i}.dat"), "", Ct);
        }

        Assert.AreEqual(folder, (await FindAsync()).Single().Folder);
    }

    [TestMethod]
    public async Task A_folder_that_cannot_be_listed_is_kept()
    {
        string folder = InstalledSteam().Common("Locked Game");
        _bed.Deny(Path.GetRelativePath(_bed.Root, folder));

        Assert.AreEqual(folder, (await FindAsync()).Single().Folder);
    }

    [TestMethod]
    [DataRow(228980, "Steamworks Common Redistributables")]
    [DataRow(250820, "SteamVR")]
    [DataRow(1493710, "Proton Experimental")]
    [DataRow(3658110, "Proton 10.0")]
    [DataRow(1628350, "Steam Linux Runtime 3.0 (sniper)")]
    [DataRow(228980, "Renamed Redistributables")]
    public async Task Steams_own_tools_are_not_offered_as_games(int appId, string name)
    {
        _ = InstalledSteam().CommonGame("Tool");
        Manifest(SteamRoot, appId, name, "Tool", "4");

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task A_manifest_without_a_name_gives_the_user_config_name_and_else_the_folder_name()
    {
        _ = InstalledSteam().Common("Folder A");
        _ = InstalledSteam().Common("Folder B");
        _ = _bed.WriteFile("Steam/steamapps/appmanifest_1.acf", "\"AppState\" { \"appid\" \"1\" \"installdir\" \"Folder A\" \"StateFlags\" \"4\" \"UserConfig\" { \"name\" \"User Name\" } }");
        _ = _bed.WriteFile("Steam/steamapps/appmanifest_2.acf", "\"AppState\" { \"appid\" \"2\" \"installdir\" \"Folder B\" \"StateFlags\" \"4\" \"name\" \"\" }");

        StoreBed.AssertNames(await FindAsync(), "User Name", "Folder B");
    }

    [TestMethod]
    public async Task A_name_with_escaped_quotes_reads_as_steam_shows_it()
    {
        _ = InstalledSteam().Common("Quoted");
        _ = _bed.WriteFile("Steam/steamapps/appmanifest_5.acf", "\"AppState\"\n{\n\t\"appid\"\t\t\"5\"\n\t\"name\"\t\t\"The \\\"Quoted\\\" Game\"\n\t\"StateFlags\"\t\t\"4\"\n\t\"installdir\"\t\t\"Quoted\"\n}\n");

        Assert.AreEqual("The \"Quoted\" Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task One_unreadable_manifest_leaves_the_others_and_its_folder_falls_back_to_the_executable_rule()
    {
        _ = InstalledSteam().Common("Good");
        _ = InstalledSteam().CommonGame("Broken");
        Manifest(SteamRoot, 1, "Good Game", "Good", "4");
        _ = _bed.WriteFile("Steam/steamapps/appmanifest_2.acf", "\"AppState\" { \"installdir\" \"Broken\" ");

        StoreBed.AssertNames(await FindAsync(), "Good Game", "Broken");
    }

    [TestMethod]
    public async Task A_manifest_still_being_written_by_steam_is_not_read()
    {
        _ = InstalledSteam().Common("Half Written");
        _ = _bed.WriteFile("Steam/steamapps/appmanifest_3.acf.tmp", Acf(3, "Half Written", "Half Written", "4"));

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task Games_in_every_library_of_the_current_library_file_are_listed()
    {
        _ = InstalledSteam().Common("Root Game");
        Manifest(SteamRoot, 1, "Root Game", "Root Game", "4");
        string library = Library("Lib" + '\\' + "Two");
        _ = _bed.Folder(Path.Join(Path.GetRelativePath(_bed.Root, library), "steamapps", "common", "Far Game"));
        Manifest(library, 2, "Far Game", "Far Game", "4");
        LibraryFile($$"""
            "libraryfolders"
            {
                "0" { "path" "{{Escaped(SteamRoot)}}" "apps" { "1" "100" } }
                "1" { "path" "{{Escaped(library)}}" "label" "" "apps" { "2" "200" } }
            }
            """);

        StoreBed.AssertNames(await FindAsync(), "Root Game", "Far Game");
    }

    [TestMethod]
    public async Task Games_in_every_library_of_the_old_library_file_are_listed()
    {
        _ = InstalledSteam();
        string library = Library("OldLib");
        _ = _bed.Folder("OldLib/steamapps/common/Old Game");
        Manifest(library, 2, "Old Game", "Old Game", "4");
        LibraryFile($$"""
            "LibraryFolders"
            {
                "TimeNextStatsReport" "1700000000"
                "ContentStatsID" "-123"
                "1" "{{Escaped(library)}}"
            }
            """);

        Assert.AreEqual("Old Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task A_listed_library_that_is_missing_or_has_no_common_folder_is_ignored()
    {
        _ = InstalledSteam().Common("Root Game");
        Manifest(SteamRoot, 1, "Root Game", "Root Game", "4");
        string bare = _bed.Folder("Bare/steamapps");
        _ = _bed.WriteFile("NotAFolder", "x");
        LibraryFile($$"""
            "libraryfolders"
            {
                "1" { "path" "{{Escaped(_bed.PathOf("Gone"))}}" }
                "2" { "path" "{{Escaped(Path.GetDirectoryName(bare)!)}}" }
                "3" { "path" "{{Escaped(_bed.PathOf("NotAFolder"))}}" }
                "4" { "path" "relative\\library" }
                "5" { "nopath" "x" }
            }
            """);

        Assert.AreEqual("Root Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task A_malformed_library_file_still_lists_the_games_in_steams_own_folder()
    {
        _ = InstalledSteam().Common("Root Game");
        Manifest(SteamRoot, 1, "Root Game", "Root Game", "4");
        LibraryFile("\"libraryfolders\" { \"1\" { \"path\" ");

        Assert.AreEqual("Root Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task A_library_reached_through_a_link_and_directly_is_listed_once()
    {
        _ = InstalledSteam();
        string library = Library("RealLib");
        _ = _bed.Folder("RealLib/steamapps/common/Linked Game");
        Manifest(library, 2, "Linked Game", "Linked Game", "4");
        string link = _bed.Link("LinkLib", "RealLib");
        LibraryFile($$"""
            "libraryfolders"
            {
                "1" { "path" "{{Escaped(library)}}" }
                "2" { "path" "{{Escaped(link)}}" }
            }
            """);

        Assert.AreEqual("Linked Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task Steam_is_found_through_the_installers_registry_value()
    {
        string root = Library("Elsewhere/Steam");
        _ = _bed.Folder("Elsewhere/Steam/steamapps/common/Game");
        Manifest(root, 1, "Game", "Game", "4");
        _ = _bed.Registry.Machine32(@"SOFTWARE\Valve\Steam", "InstallPath", root);

        Assert.AreEqual("Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task Steam_is_found_in_its_default_folder_when_the_registry_says_nothing()
    {
        string root = Library("Program Files (x86)/Steam");
        _ = _bed.Folder("Program Files (x86)/Steam/steamapps/common/Game");
        Manifest(root, 1, "Game", "Game", "4");
        _ = _bed.Registry.Set(RegistryRoot.CurrentUser, RegistryView.Registry64, @"Software\Valve\Steam", "SteamPath", _bed.PathOf("Uninstalled"));

        Assert.AreEqual("Game", (await FindAsync()).Single().Name);
    }

    [TestMethod]
    public async Task A_cancelled_search_stops_with_cancellation()
    {
        _ = InstalledSteam().Common("Game");
        Manifest(SteamRoot, 1, "Game", "Game", "4");

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => new SteamSource(_bed.Registry, _bed.Folders).FindAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task No_steam_on_the_machine_finds_nothing()
    {
        SteamSource source = new(_bed.Registry, new HostFolders(null, null, null, null, null));

        Assert.IsEmpty(await StoreBed.FindAsync(source, Ct));
    }

    [TestMethod]
    public void The_source_says_it_is_steam() =>
        Assert.AreEqual(GameSource.Steam, new SteamSource(_bed.Registry, _bed.Folders).Source);

    private Task<StoreGame[]> FindAsync() => StoreBed.FindAsync(new SteamSource(_bed.Registry, _bed.Folders), Ct);

    private SteamTree InstalledSteam()
    {
        _ = Library("Steam");
        _ = _bed.Registry.Set(RegistryRoot.CurrentUser, RegistryView.Registry64, @"Software\Valve\Steam", "SteamPath", SteamRoot);
        return new(_bed, Path.Join(SteamRoot, "steamapps", "common"));
    }

    private string Library(string relative) => Path.GetDirectoryName(Path.GetDirectoryName(_bed.Folder(Path.Join(relative, "steamapps", "common")))!)!;

    private void LibraryFile(string text) => _ = _bed.WriteFile("Steam/steamapps/libraryfolders.vdf", text);

    private static void Manifest(string library, int appId, string name, string installDir, string? flags) =>
        File.WriteAllText(Path.Join(library, "steamapps", $"appmanifest_{appId}.acf"), Acf(appId, name, installDir, flags));

    private static string Acf(int appId, string name, string installDir, string? flags) =>
        $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{appId}\"\n\t\"name\"\t\t\"{name}\"\n"
        + (flags is null ? "" : $"\t\"StateFlags\"\t\t\"{flags}\"\n")
        + $"\t\"installdir\"\t\t\"{installDir}\"\n}}\n";

    // Steam doubles every backslash in the paths it writes.
    private static string Escaped(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private sealed class SteamTree(StoreBed bed, string common)
    {
        public string Common(string name) => bed.Folder(Path.Join(Path.GetRelativePath(bed.Root, common), name));

        public string CommonGame(string name) => bed.Game(Path.Join(Path.GetRelativePath(bed.Root, common), name));
    }
}
