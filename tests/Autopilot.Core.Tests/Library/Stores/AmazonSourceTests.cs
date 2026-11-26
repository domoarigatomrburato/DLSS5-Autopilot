using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;
using Microsoft.Data.Sqlite;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class AmazonSourceTests(TestContext testContext) : IDisposable
{
    private const string Database = "LocalAppData/Amazon Games/Data/Games/Sql/GameInstallInfo.sqlite";

    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task Installed_games_come_from_the_apps_database_under_their_product_title()
    {
        string folder = _bed.Folder("Amazon Games/Library/Fuel Game");
        string untitled = _bed.Folder("D/Untitled");
        _ = _bed.Folder("Amazon Games/Library/Not In Database");
        await DatabaseAsync(
            amazonShape: true,
            ("a", folder, 1, "Fuel Game Deluxe"),
            ("b", untitled, 1, null),
            ("c", _bed.Folder("D/Uninstalled"), 0, "Uninstalled"),
            ("d", _bed.PathOf("Gone"), 1, "Gone"));

        StoreGame[] games = await FindAsync(_bed.Drive("C"));

        StoreBed.AssertNames(games, "Fuel Game Deluxe", "Untitled");
        Assert.IsTrue(games.All(game => game.Source == GameSource.Amazon));
    }

    [TestMethod]
    public async Task A_database_of_another_shape_falls_back_to_the_library_folders()
    {
        _ = _bed.Folder("LocalAppData/Amazon Games/Library/Local Game");
        await DatabaseAsync(amazonShape: false);

        StoreBed.AssertNames(await FindAsync(), "Local Game");
    }

    [TestMethod]
    public async Task A_corrupt_database_falls_back_to_the_library_folders()
    {
        _ = _bed.Folder("LocalAppData/Amazon Games/Library/Local Game");
        _ = _bed.WriteFile(Database, "this is not a database file, only text that is long enough to be read");

        StoreBed.AssertNames(await FindAsync(), "Local Game");
    }

    [TestMethod]
    public async Task Without_a_database_the_library_folders_of_the_app_and_every_fixed_or_external_drive_are_listed()
    {
        _ = _bed.Folder("LocalAppData/Amazon Games/Library/Local Game");
        DriveRoot fixedDrive = _bed.Drive("D");
        _ = _bed.Folder("D/Amazon Games/Library/Drive Game");
        DriveRoot removable = _bed.Drive("U", DriveKind.Removable);
        _ = _bed.Folder("U/Amazon Games/Library/External Game");
        DriveRoot network = _bed.Drive("N", DriveKind.Network);
        _ = _bed.Folder("N/Amazon Games/Library/Share Game");
        DriveRoot other = _bed.Drive("O", DriveKind.Other);
        _ = _bed.Folder("O/Amazon Games/Library/Other Game");

        StoreBed.AssertNames(await FindAsync(fixedDrive, removable, network, other), "Local Game", "Drive Game", "External Game");
    }

    [TestMethod]
    public async Task No_local_app_data_folder_still_searches_the_drives()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.Folder("D/Amazon Games/Library/Drive Game");
        AmazonSource source = new(new FakeDrives(drive), new HostFolders(null, null, null, null, null));

        StoreBed.AssertNames(await StoreBed.FindAsync(source, Ct), "Drive Game");
    }

    [TestMethod]
    public void The_source_says_it_is_amazon() => Assert.AreEqual(GameSource.Amazon, new AmazonSource(new FakeDrives(), _bed.Folders).Source);

    private Task<StoreGame[]> FindAsync(params DriveRoot[] drives) => StoreBed.FindAsync(new AmazonSource(new FakeDrives(drives), _bed.Folders), Ct);

    private async Task DatabaseAsync(bool amazonShape, params (string Id, string Folder, int Installed, string? Title)[] rows)
    {
        string path = _bed.PathOf(Database);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using SqliteConnection connection = new(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        await connection.OpenAsync(Ct);
        await using SqliteCommand create = connection.CreateCommand();
        if (amazonShape)
        {
            create.CommandText = "CREATE TABLE DbSet (Id TEXT, InstallDirectory TEXT, Installed INTEGER, ProductTitle TEXT)";
        }
        else
        {
            create.CommandText = "CREATE TABLE Other (Id TEXT)";
        }

        _ = await create.ExecuteNonQueryAsync(Ct);
        foreach ((string id, string folder, int installed, string? title) in rows)
        {
            await using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO DbSet (Id, InstallDirectory, Installed, ProductTitle) VALUES ($id, $folder, $installed, $title)";
            _ = insert.Parameters.AddWithValue("$id", id);
            _ = insert.Parameters.AddWithValue("$folder", folder);
            _ = insert.Parameters.AddWithValue("$installed", installed);
            _ = insert.Parameters.AddWithValue("$title", (object?)title ?? DBNull.Value);
            _ = await insert.ExecuteNonQueryAsync(Ct);
        }
    }
}
