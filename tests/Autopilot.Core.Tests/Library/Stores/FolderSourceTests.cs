using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class FolderSourceTests(TestContext testContext) : IDisposable
{
    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    [DataRow("Games")]
    [DataRow("Game")]
    [DataRow("Oyunlar")]
    [DataRow("Juegos")]
    [DataRow("Spiele")]
    [DataRow("Jeux")]
    [DataRow("Giochi")]
    [DataRow("My Games")]
    [DataRow("PC Games")]
    [DataRow("Installed Games")]
    public async Task A_game_in_a_games_folder_at_a_drive_root_is_listed_under_its_folder_name(string games)
    {
        DriveRoot drive = _bed.Drive("D");
        string folder = _bed.Folder($"D/{games}/Bayonetta");

        StoreGame game = (await FindAsync(drive)).Single();

        Assert.AreEqual(new StoreGame("Bayonetta", folder, GameSource.Folder, null), game);
    }

    [TestMethod]
    public async Task Only_the_first_level_below_a_games_folder_is_a_game()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.Folder("D/Games/Outer/Inner");

        StoreBed.AssertNames(await FindAsync(drive), "Outer");
    }

    [TestMethod]
    [DataRow(".hidden")]
    [DataRow("$RECYCLE.BIN")]
    [DataRow("SteamLibrary")]
    [DataRow("steamapps")]
    [DataRow("Epic Games")]
    [DataRow("GOG Galaxy")]
    [DataRow("EA Games")]
    [DataRow("Ubisoft")]
    [DataRow("XboxGames")]
    [DataRow("Amazon Games")]
    [DataRow("Battle.net")]
    [DataRow("common")]
    [DataRow("Riot Games")]
    [DataRow("ROCKSTAR GAMES")]
    public async Task Hidden_folders_and_the_stores_own_libraries_are_not_games(string name)
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.Folder($"D/Games/{name}");
        _ = _bed.Folder("D/Games/Real Game");

        StoreBed.AssertNames(await FindAsync(drive), "Real Game");
    }

    [TestMethod]
    [DataRow(DriveKind.Removable)]
    [DataRow(DriveKind.Network)]
    [DataRow(DriveKind.Other)]
    public async Task Only_fixed_drives_are_searched(DriveKind kind)
    {
        DriveRoot drive = _bed.Drive("U", kind);
        _ = _bed.Folder("U/Games/Backup");

        Assert.IsEmpty(await FindAsync(drive));
    }

    [TestMethod]
    public async Task A_drive_that_is_not_ready_or_cannot_be_read_does_not_stop_the_others()
    {
        DriveRoot missing = new(_bed.PathOf("NoCard") + Path.DirectorySeparatorChar, DriveKind.Fixed);
        DriveRoot locked = _bed.Drive("Locked");
        _ = _bed.Folder("Locked/Games/Hidden");
        _bed.Deny("Locked");
        DriveRoot good = _bed.Drive("E");
        _ = _bed.Folder("E/Games/Found");

        StoreBed.AssertNames(await FindAsync(missing, locked, good), "Found");
    }

    [TestMethod]
    public async Task A_folder_and_a_link_to_it_are_listed_once()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.Folder("D/Games/Real");
        _ = _bed.Link("D/Games/Alias", "D/Games/Real");

        Assert.HasCount(1, await FindAsync(drive));
    }

    [TestMethod]
    public async Task A_cancelled_search_stops_with_cancellation()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.Folder("D/Games/Game");

        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => new FolderSource(new FakeDrives(drive)).FindAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public async Task No_drives_find_nothing() => Assert.IsEmpty(await FindAsync());

    [TestMethod]
    public void The_source_says_it_is_plain_folders() => Assert.AreEqual(GameSource.Folder, new FolderSource(new FakeDrives()).Source);

    private Task<StoreGame[]> FindAsync(params DriveRoot[] drives) => StoreBed.FindAsync(new FolderSource(new FakeDrives(drives)), Ct);
}
