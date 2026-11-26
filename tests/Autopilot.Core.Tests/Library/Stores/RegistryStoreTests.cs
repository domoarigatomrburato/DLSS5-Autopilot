using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class RegistryStoreTests(TestContext testContext) : IDisposable
{
    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task Ea_games_come_from_both_registry_views_and_the_ea_games_folders_named_after_the_folder()
    {
        string registered = _bed.Folder("D/Origin Games/Mass Effect");
        string wow = _bed.Folder("D/EA/Battlefield 2042");
        string defaultFolder = _bed.Folder("Program Files/EA Games/Dead Space");
        string x86Folder = _bed.Folder("Program Files (x86)/EA Games/Crysis");
        _ = _bed.Registry.Machine(@"SOFTWARE\Electronic Arts\Mass Effect", "Install Dir", registered + Path.DirectorySeparatorChar);
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Battlefield 2042", "Install Dir", wow);
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Gone", "Install Dir", _bed.PathOf("Gone"));
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\EA Desktop", "InstallLocation", _bed.PathOf("Program Files"));
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Odd", "Install Dir", 12);
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Relative", "Install Dir", "relative");

        StoreGame[] games = await StoreBed.FindAsync(new EaSource(_bed.Registry, _bed.Folders), Ct);

        StoreBed.AssertNames(games, "Mass Effect", "Battlefield 2042", "Dead Space", "Crysis");
        CollectionAssert.IsSubsetOf(new[] { wow, defaultFolder, x86Folder }, games.Select(game => game.Folder).ToArray());
        Assert.IsTrue(games.All(game => game.Source == GameSource.Ea && game.StoreExecutable is null));
    }

    [TestMethod]
    public async Task Ea_without_program_files_reads_the_registry_only()
    {
        string registered = _bed.Folder("D/Mass Effect");
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Mass Effect", "Install Dir", registered);

        StoreGame[] games = await StoreBed.FindAsync(new EaSource(_bed.Registry, new HostFolders(null, null, null, null, null)), Ct);

        StoreBed.AssertNames(games, "Mass Effect");
    }

    [TestMethod]
    public async Task Ubisoft_games_come_from_the_32_bit_view_named_after_the_folder()
    {
        string folder = _bed.Folder("Ubisoft Game Launcher/games/Anno 1800");
        _ = _bed.Registry.Machine32(@"SOFTWARE\Ubisoft\Launcher\Installs\4553", "InstallDir", folder.Replace('\\', '/') + "/");
        _ = _bed.Registry.Machine32(@"SOFTWARE\Ubisoft\Launcher\Installs\9", "InstallDir", _bed.PathOf("Gone"));
        _ = _bed.Registry.Machine(@"SOFTWARE\Ubisoft\Launcher\Installs\1", "InstallDir", _bed.Folder("Other"));

        StoreGame game = (await StoreBed.FindAsync(new UbisoftSource(_bed.Registry), Ct)).Single();

        Assert.AreEqual("Anno 1800", game.Name);
        Assert.AreEqual(GameSource.Ubisoft, game.Source);
    }

    [TestMethod]
    public async Task Ubisoft_falls_back_to_the_64_bit_view_when_the_32_bit_one_is_empty()
    {
        string folder = _bed.Folder("Ubisoft/Far Cry 6");
        _ = _bed.Registry.Machine(@"SOFTWARE\Ubisoft\Launcher\Installs\5266", "InstallDir", folder);

        StoreBed.AssertNames(await StoreBed.FindAsync(new UbisoftSource(_bed.Registry), Ct), "Far Cry 6");
    }

    [TestMethod]
    public async Task Battle_net_games_come_from_uninstall_keys_with_a_product_uid_under_their_display_name()
    {
        string wow = _bed.Folder("Battle/World of Warcraft");
        string diablo = _bed.Folder("Battle/Diablo IV");
        _ = _bed.Registry.Uninstall("World of Warcraft", RegistryRoot.LocalMachine, RegistryView.Registry32, "World of Warcraft", wow, "Blizzard Entertainment", "\"C:\\ProgramData\\Battle.net\\Agent\\Blizzard Uninstaller.exe\" --lang=enUS --uid=wow_enus --displayname=\"World of Warcraft\"");
        _ = _bed.Registry.Uninstall("Diablo IV", RegistryRoot.CurrentUser, RegistryView.Registry64, null, diablo, null, "\"C:\\ProgramData\\Battle.net\\Agent\\Blizzard Uninstaller.exe\" --uid=fenris");

        StoreGame[] games = await StoreBed.FindAsync(new BattleNetSource(_bed.Registry), Ct);

        StoreBed.AssertNames(games, "World of Warcraft", "Diablo IV");
        Assert.IsTrue(games.All(game => game.Source == GameSource.BattleNet));
    }

    [TestMethod]
    [DataRow("Overwatch Test", "Overwatch Test", "--uid=prometheus_test")]
    [DataRow("WoW Beta", "World of Warcraft Beta", "--uid=wow_beta")]
    [DataRow("Battle.net", "Battle.net", "--uid=battle.net")]
    [DataRow("Battle.net", "Battle.net", "")]
    [DataRow("Client", "Battle.net App", "--uid=battle.net")]
    [DataRow("Unrelated", "Unrelated", "--uid=x")]
    public async Task Battle_net_test_and_beta_clients_the_client_itself_and_other_programs_are_not_games(string key, string name, string uid)
    {
        string folder = _bed.Folder("Battle/Thing");
        string command = key == "Unrelated" ? $"C:\\Other\\uninstall.exe {uid}" : $"\"C:\\ProgramData\\Battle.net\\Agent\\Blizzard Uninstaller.exe\" {uid} --lang=enUS";
        _ = _bed.Registry.Uninstall(key, displayName: name, installLocation: folder, uninstallString: command);

        Assert.IsEmpty(await StoreBed.FindAsync(new BattleNetSource(_bed.Registry), Ct));
    }

    [TestMethod]
    public async Task Battle_net_entries_without_a_folder_on_disk_are_not_listed()
    {
        _ = _bed.Registry.Uninstall("Gone", displayName: "Gone", installLocation: _bed.PathOf("Gone"), uninstallString: "Battle.net\\x.exe --uid=gone ");
        _ = _bed.Registry.Uninstall("NoFolder", displayName: "NoFolder", uninstallString: "Battle.net\\x.exe --uid=none ");

        Assert.IsEmpty(await StoreBed.FindAsync(new BattleNetSource(_bed.Registry), Ct));
    }

    [TestMethod]
    public async Task Rockstar_games_come_from_the_launchers_uninstall_keys_under_their_display_name()
    {
        string rdr2 = _bed.Folder("Rockstar Games/Red Dead Redemption 2");
        string gta = _bed.Folder("Rockstar Games/GTAV");
        _ = _bed.Registry.Uninstall("{5EFC6C07-6B87-43FC-9524-F9E967241741}", RegistryRoot.LocalMachine, RegistryView.Registry32, "Red Dead Redemption 2", rdr2, "Rockstar Games", "\"C:\\Program Files\\Rockstar Games\\Launcher\\Launcher.exe\" -enableFullMode -uninstall=rdr2");
        _ = _bed.Registry.Uninstall("GTA", uninstallString: "\"C:\\Program Files\\Rockstar Games\\Launcher\\uninstall.exe\" -uninstall=gta5", installLocation: gta);

        StoreGame[] games = await StoreBed.FindAsync(new RockstarSource(_bed.Registry), Ct);

        StoreBed.AssertNames(games, "Red Dead Redemption 2", "GTAV");
        Assert.IsTrue(games.All(game => game.Source == GameSource.Rockstar));
    }

    [TestMethod]
    [DataRow("Rockstar Games Launcher", "launcher")]
    [DataRow("Rockstar Games Social Club", "socialclub")]
    [DataRow("Social Club", "sc")]
    [DataRow("Launcher", "x")]
    public async Task Rockstars_launcher_and_social_club_are_not_games(string name, string title)
    {
        string folder = _bed.Folder("Rockstar Games/Launcher");
        _ = _bed.Registry.Uninstall("Rockstar Games " + title, displayName: name, installLocation: folder, uninstallString: $"\"C:\\Rockstar Games\\Launcher\\uninstall.exe\" -uninstall={title}");

        Assert.IsEmpty(await StoreBed.FindAsync(new RockstarSource(_bed.Registry), Ct));
    }

    [TestMethod]
    public async Task Rockstar_ignores_other_programs_and_missing_folders()
    {
        _ = _bed.Registry.Uninstall("Other", displayName: "Other", installLocation: _bed.Folder("Other"), uninstallString: "C:\\Other\\setup.exe /remove");
        _ = _bed.Registry.Uninstall("NoCommand", displayName: "NoCommand", installLocation: _bed.Folder("NoCommand"));
        _ = _bed.Registry.Uninstall("Gone", displayName: "Gone", installLocation: _bed.PathOf("Gone"), uninstallString: "Launcher.exe -uninstall=gone");

        Assert.IsEmpty(await StoreBed.FindAsync(new RockstarSource(_bed.Registry), Ct));
    }

    [TestMethod]
    public void Each_registry_source_says_which_store_it_reads()
    {
        Assert.AreEqual(GameSource.Ea, new EaSource(_bed.Registry, _bed.Folders).Source);
        Assert.AreEqual(GameSource.Ubisoft, new UbisoftSource(_bed.Registry).Source);
        Assert.AreEqual(GameSource.BattleNet, new BattleNetSource(_bed.Registry).Source);
        Assert.AreEqual(GameSource.Rockstar, new RockstarSource(_bed.Registry).Source);
    }
}
