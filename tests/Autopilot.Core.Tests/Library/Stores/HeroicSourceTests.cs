using System.Text.Json;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class HeroicSourceTests(TestContext testContext) : IDisposable
{
    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task Games_from_legendary_heroics_gog_store_and_nile_are_listed()
    {
        string epic = _bed.Folder("Heroic/Hades");
        string dlc = _bed.Folder("Heroic/Hades DLC");
        string gog = _bed.Folder("Heroic/Gwent");
        string amazon = _bed.Folder("Heroic/Fuel Game");
        Write("legendaryConfig/legendary/installed.json", new Dictionary<string, object>
        {
            ["Min"] = new { app_name = "Min", title = "Hades", install_path = epic, is_dlc = false },
            ["Dlc"] = new { app_name = "Dlc", title = "Hades DLC", install_path = dlc, is_dlc = true },
            ["Gone"] = new { app_name = "Gone", title = "Gone", install_path = _bed.PathOf("Gone") },
        });
        Write("gog_store/installed.json", new { installed = new object[] { new { appName = "1", install_path = gog }, 7 } });
        Write("nile_config/nile/installed.json", new object[] { new { id = "amzn", path = amazon }, new { id = "nopath" } });

        StoreGame[] games = await StoreBed.FindAsync(new HeroicSource(_bed.Folders), Ct);

        StoreBed.AssertNames(games, "Hades", "Gwent", "Fuel Game");
        Assert.IsTrue(games.All(game => game.Source == GameSource.Heroic && game.StoreExecutable is null));
    }

    [TestMethod]
    [DataRow("{ broken")]
    [DataRow("42")]
    [DataRow("\"text\"")]
    public async Task A_malformed_list_leaves_the_others(string text)
    {
        string gog = _bed.Folder("Heroic/Gwent");
        _ = _bed.WriteFile("AppData/heroic/legendaryConfig/legendary/installed.json", text);
        Write("gog_store/installed.json", new { installed = new[] { new { install_path = gog } } });

        StoreBed.AssertNames(await StoreBed.FindAsync(new HeroicSource(_bed.Folders), Ct), "Gwent");
    }

    [TestMethod]
    public async Task No_app_data_folder_skips_the_source()
    {
        HeroicSource source = new(new HostFolders(_bed.PathOf("ProgramData"), null, null, null, null));

        Assert.IsEmpty(await StoreBed.FindAsync(source, Ct));
    }

    [TestMethod]
    public void The_source_says_it_is_heroic() => Assert.AreEqual(GameSource.Heroic, new HeroicSource(_bed.Folders).Source);

    private void Write(string relative, object content) => _ = _bed.WriteFile($"AppData/heroic/{relative}", JsonSerializer.Serialize(content));
}
