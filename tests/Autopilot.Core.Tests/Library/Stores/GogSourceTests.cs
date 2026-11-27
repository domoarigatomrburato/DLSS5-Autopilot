using System.Text.Json;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class GogSourceTests(TestContext testContext) : IDisposable
{
    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task An_installed_game_is_listed_under_gogs_name_with_its_primary_play_task()
    {
        string folder = _bed.Game("GOG Games/Witcher 3", @"bin/x64/witcher3.exe");
        Installed("1207664643", folder, "The Witcher 3 (uninstaller name)");
        _ = _bed.Registry.Machine32(@"SOFTWARE\GOG.com\Games\1207664643", "gameName", "The Witcher 3: Wild Hunt");
        Info(folder, "1207664643", new { gameId = "1207664643", rootGameId = "1207664643", name = "Witcher Info", playTasks = new object[] { new { isPrimary = false, type = "FileTask", path = "other.exe" }, new { isPrimary = true, type = "FileTask", path = @"bin\x64\witcher3.exe" } } });

        StoreGame game = (await FindAsync()).Single();

        Assert.AreEqual(new StoreGame("The Witcher 3: Wild Hunt", folder, GameSource.Gog, Path.Join(folder, "bin", "x64", "witcher3.exe")), game);
    }

    [TestMethod]
    public async Task Without_galaxys_name_the_uninstall_name_and_then_the_info_name_and_then_the_folder_name_are_used()
    {
        string first = _bed.Folder("GOG Games/A");
        string second = _bed.Folder("GOG Games/B");
        string third = _bed.Folder("GOG Games/C");
        Installed("1", first, "Uninstall Name");
        Installed("2", second, null);
        Info(second, "2", new { name = "Info Name", playTasks = new[] { new { isPrimary = true, type = "URLTask" } } });
        Installed("3", third, " ");
        _ = _bed.Registry.Machine(@"SOFTWARE\GOG.com\Games\3", "gameName", 42);

        StoreBed.AssertNames(await FindAsync(), "Uninstall Name", "Info Name", "C");
    }

    [TestMethod]
    public async Task A_dlc_sharing_its_games_folder_is_not_listed_as_a_game()
    {
        string folder = _bed.Folder("GOG Games/Cyberpunk");
        Installed("1111", folder, "Phantom Liberty");
        Info(folder, "1111", new { gameId = "1111", rootGameId = "2222" });
        Installed("2222", folder, "Cyberpunk 2077");
        Info(folder, "2222", new { gameId = "2222", rootGameId = "2222", playTasks = new[] { new { isPrimary = true, type = "FileTask", path = "game.exe" } } });

        StoreBed.AssertNames(await FindAsync(), "Cyberpunk 2077");
    }

    [TestMethod]
    public async Task An_info_file_with_no_primary_play_task_is_dlc()
    {
        string folder = _bed.Folder("GOG Games/Pack");
        Installed("5", folder, "Soundtrack");
        Info(folder, "5", new { gameId = "5", playTasks = new[] { new { isPrimary = false, type = "FileTask", path = "x.exe" } } });

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("{ broken")]
    [DataRow("[]")]
    public async Task A_missing_or_unreadable_info_file_keeps_the_game(string? info)
    {
        string folder = _bed.Folder("GOG Games/Old");
        Installed("7", folder, "Old Game");
        if (info is not null)
        {
            _ = _bed.WriteFile("GOG Games/Old/goggame-7.info", info);
        }

        StoreBed.AssertNames(await FindAsync(), "Old Game");
    }

    [TestMethod]
    [DataRow("GOGPACKWITCHER_is1", "GOG.com")]
    [DataRow("1207664643", "GOG.com")]
    [DataRow("1207664643_is1", "Someone Else")]
    [DataRow("1207664643_is1", null)]
    public async Task Only_gogs_own_installer_entries_are_games(string keyName, string? publisher)
    {
        string folder = _bed.Folder("GOG Games/Game");
        _ = _bed.Registry.Uninstall(keyName, displayName: "Game", installLocation: folder, publisher: publisher);

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task An_entry_whose_folder_is_gone_is_not_listed_and_entries_in_every_view_are_read()
    {
        Installed("1", _bed.PathOf("Gone"), "Gone");
        string folder = _bed.Folder("GOG Games/Here");
        _ = _bed.Registry.Uninstall("2_is1", RegistryRoot.CurrentUser, RegistryView.Registry32, "Here", folder, "GOG.com");

        StoreBed.AssertNames(await FindAsync(), "Here");
    }

    [TestMethod]
    public async Task An_info_file_saved_with_a_byte_order_mark_is_read()
    {
        string folder = _bed.Game("GOG Games/Marked", "marked.exe");
        Installed("7", folder, null);
        Info(folder, "7", new { name = "Marked Info", playTasks = new[] { new { isPrimary = true, type = "FileTask", path = "marked.exe" } } });
        TestFiles.AddByteOrderMark(Path.Join(folder, "goggame-7.info"));

        Assert.AreEqual(new StoreGame("Marked Info", folder, GameSource.Gog, Path.Join(folder, "marked.exe")), (await FindAsync()).Single());
    }

    [TestMethod]
    public void The_source_says_it_is_gog() => Assert.AreEqual(GameSource.Gog, new GogSource(_bed.Registry).Source);

    private Task<StoreGame[]> FindAsync() => StoreBed.FindAsync(new GogSource(_bed.Registry), Ct);

    private void Installed(string id, string folder, string? name) =>
        _bed.Registry.Uninstall($"{id}_is1", RegistryRoot.LocalMachine, RegistryView.Registry32, name, folder, "GOG.com");

    private void Info(string folder, string id, object info) =>
        _ = _bed.WriteFile(Path.Join(Path.GetRelativePath(_bed.Root, folder), $"goggame-{id}.info"), JsonSerializer.Serialize(info));
}
