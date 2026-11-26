using System.Text.Json;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class EpicSourceTests(TestContext testContext) : IDisposable
{
    private const string Manifests = "ProgramData/Epic/EpicGamesLauncher/Data/Manifests";

    // The cap on store files, 16 MiB.
    private const long MaxStoreFileBytes = 16 * 1024 * 1024;

    private static readonly string[] _launchableAddOn = ["addons", "addons/launchable"];

    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task An_installed_game_is_listed_under_its_display_name_with_the_launchers_executable()
    {
        string folder = _bed.Game("Epic Games/Alan Wake 2", @"AlanWake2.exe");
        Item("game", folder, ("DisplayName", "Alan Wake 2"), ("LaunchExecutable", "AlanWake2.exe"), ("bIsApplication", true), ("bIsExecutable", true));

        StoreGame game = (await FindAsync()).Single();

        Assert.AreEqual(new StoreGame("Alan Wake 2", folder, GameSource.Epic, Path.Join(folder, "AlanWake2.exe")), game);
    }

    [TestMethod]
    [DataRow("Missing.exe")]
    [DataRow("../Outside.exe")]
    [DataRow("")]
    public async Task A_launch_executable_that_is_missing_or_outside_the_folder_is_no_pick(string launch)
    {
        string folder = _bed.Folder("Epic Games/Game");
        _ = _bed.WriteFile("Epic Games/Outside.exe");
        Item("game", folder, ("DisplayName", "Game"), ("LaunchExecutable", launch));

        Assert.IsNull((await FindAsync()).Single().StoreExecutable);
    }

    [TestMethod]
    [DataRow("bIsApplication", false)]
    [DataRow("bIsExecutable", false)]
    [DataRow("bIsIncompleteInstall", true)]
    public async Task Engine_plugins_content_packs_and_incomplete_installs_are_not_games(string flag, bool value)
    {
        string folder = _bed.Folder("Epic Games/Thing");
        Item("thing", folder, ("DisplayName", "Thing"), (flag, value));

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task An_older_manifest_without_the_flags_is_a_game()
    {
        string folder = _bed.Folder("Epic Games/Old");
        Item("old", folder, ("DisplayName", "Old Game"));

        StoreBed.AssertNames(await FindAsync(), "Old Game");
    }

    [TestMethod]
    [DataRow("AppCategories", new[] { "public", "addons" })]
    [DataRow("AppCategories", new[] { "plugins", "engine" })]
    [DataRow("AppCategories", new[] { "plugins/engine" })]
    [DataRow("CompatibleApps", new[] { "UE_5.3", "UE_5.4" })]
    public async Task Add_ons_and_unreal_engine_plugins_are_not_games(string field, string[] values)
    {
        string folder = _bed.Folder("Epic Games/Plugin");
        Item("plugin", folder, ("DisplayName", "Plugin"), (field, values));

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task A_launchable_add_on_and_a_technical_type_other_than_a_plugin_are_games()
    {
        string first = _bed.Folder("Epic Games/Launchable");
        string second = _bed.Folder("Epic Games/Typed");
        Item("first", first, ("DisplayName", "Launchable"), ("AppCategories", _launchableAddOn));
        Item("second", second, ("DisplayName", "Typed"), ("TechnicalType", "games"));

        StoreBed.AssertNames(await FindAsync(), "Launchable", "Typed");
    }

    [TestMethod]
    public async Task A_technical_type_of_engine_plugin_is_not_a_game()
    {
        string folder = _bed.Folder("Epic Games/Bridge");
        Item("bridge", folder, ("DisplayName", "Bridge"), ("TechnicalType", "plugins/engine"));

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task A_folder_shared_with_its_dlc_is_named_after_the_main_game()
    {
        string folder = _bed.Folder("Epic Games/Hogwarts");
        Item("a-dlc", folder, ("AppName", "dlc1"), ("MainGameAppName", "main"), ("DisplayName", "Hogwarts DLC"));
        Item("b-game", folder, ("AppName", "Main"), ("MainGameAppName", "main"), ("DisplayName", "Hogwarts Legacy"));

        StoreBed.AssertNames(await FindAsync(), "Hogwarts Legacy");
    }

    [TestMethod]
    public async Task A_manifest_without_a_display_name_gives_the_folder_name()
    {
        string folder = _bed.Folder("Epic Games/Nameless");
        Item("nameless", folder);

        StoreBed.AssertNames(await FindAsync(), "Nameless");
    }

    [TestMethod]
    public async Task A_game_moved_since_its_manifest_was_written_is_found_through_the_launchers_list()
    {
        string moved = _bed.Folder("D/Moved");
        Item("moved", _bed.PathOf("Gone"), ("AppName", "MovedApp"), ("DisplayName", "Moved"));
        _ = _bed.WriteFile("ProgramData/Epic/UnrealEngineLauncher/LauncherInstalled.dat", JsonSerializer.Serialize(new
        {
            InstallationList = new object[] { new { AppName = "movedapp", InstallLocation = moved }, new { AppName = 3 }, "noise" },
        }));

        Assert.AreEqual(moved, (await FindAsync()).Single().Folder);
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ "{ \"InstallationList\": 4 }")]
    [DataRow("[]")]
    [DataRow("not json")]
    public async Task A_game_whose_folder_is_gone_is_not_listed(string launcherList)
    {
        Item("gone", _bed.PathOf("Gone"), ("AppName", "GoneApp"), ("DisplayName", "Gone"));
        _ = _bed.WriteFile("ProgramData/Epic/UnrealEngineLauncher/LauncherInstalled.dat", launcherList);

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    [DataRow("{ \"DisplayName\": ")]
    [DataRow("[1, 2]")]
    [DataRow(/*lang=json,strict*/ "{ \"DisplayName\": 5, \"InstallLocation\": 7 }")]
    public async Task One_malformed_manifest_leaves_the_others(string text)
    {
        string folder = _bed.Folder("Epic Games/Good");
        Item("good", folder, ("DisplayName", "Good"));
        _ = _bed.WriteFile($"{Manifests}/bad.item", text);
        _ = _bed.WriteFile($"{Manifests}/notes.txt", "ignored");

        StoreBed.AssertNames(await FindAsync(), "Good");
    }

    [TestMethod]
    public async Task An_oversize_manifest_is_not_read()
    {
        string folder = _bed.Folder("Epic Games/Big");
        Item("big", folder, ("DisplayName", "Big"));
        await using (FileStream file = File.OpenWrite(_bed.PathOf($"{Manifests}/big.item")))
        {
            file.SetLength(MaxStoreFileBytes + 1);
        }

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task No_program_data_folder_skips_the_source()
    {
        EpicSource source = new(new HostFolders(null, _bed.PathOf("LocalAppData"), null, null, null));

        Assert.IsEmpty(await StoreBed.FindAsync(source, Ct));
    }

    [TestMethod]
    public void The_source_says_it_is_epic() => Assert.AreEqual(GameSource.Epic, new EpicSource(_bed.Folders).Source);

    private Task<StoreGame[]> FindAsync() => StoreBed.FindAsync(new EpicSource(_bed.Folders), Ct);

    private void Item(string file, string location, params (string Name, object Value)[] fields)
    {
        Dictionary<string, object> item = new() { ["InstallLocation"] = location };
        foreach ((string name, object value) in fields)
        {
            item[name] = value;
        }

        _ = _bed.WriteFile($"{Manifests}/{file}.item", JsonSerializer.Serialize(item));
    }
}
