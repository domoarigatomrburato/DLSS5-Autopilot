using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class XboxSourceTests(TestContext testContext) : IDisposable
{
    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_game_pass_game_is_its_content_folder_named_after_the_game()
    {
        DriveRoot drive = _bed.Drive("X");
        string content = _bed.Folder("X/XboxGames/Forza Horizon 5/Content");

        Assert.AreEqual(new StoreGame("Forza Horizon 5", content, GameSource.Xbox, null), (await FindAsync(drive)).Single());
    }

    [TestMethod]
    public async Task A_game_without_a_content_folder_is_the_folder_itself()
    {
        DriveRoot drive = _bed.Drive("C");
        string folder = _bed.Folder("C/Program Files/ModifiableWindowsApps/Halo Infinite");

        Assert.AreEqual(folder, (await FindAsync(drive)).Single().Folder);
    }

    [TestMethod]
    [DataRow("GameSave")]
    [DataRow("gamesave")]
    [DataRow("Minecraft Launcher")]
    public async Task Cloud_save_containers_and_the_minecraft_launcher_are_not_games(string name)
    {
        DriveRoot drive = _bed.Drive("X");
        _ = _bed.Folder($"X/XboxGames/{name}/Content");
        _ = _bed.Folder("X/XboxGames/Starfield/Content");

        StoreBed.AssertNames(await FindAsync(drive), "Starfield");
    }

    [TestMethod]
    public async Task The_pc_executable_microsoft_game_config_names_is_the_stores_pick()
    {
        DriveRoot drive = _bed.Drive("X");
        _ = _bed.WriteFile("X/XboxGames/Game/Content/Console.exe");
        string pc = _bed.WriteFile("X/XboxGames/Game/Content/Bin/Game.exe");
        Config("""
            <?xml version="1.0" encoding="utf-8"?>
            <Game configVersion="1">
              <ExecutableList>
                <Executable Name="Console.exe" TargetDeviceFamily="XboxOne" Id="Game" />
                <Executable Name="Bin\Game.exe" TargetDeviceFamily="PC" Id="Game" />
              </ExecutableList>
            </Game>
            """);

        Assert.AreEqual(Path.GetFullPath(pc), Path.GetFullPath((await FindAsync(drive)).Single().StoreExecutable!));
    }

    [TestMethod]
    public async Task With_no_pc_entry_the_first_executable_is_the_stores_pick()
    {
        DriveRoot drive = _bed.Drive("X");
        string first = _bed.WriteFile("X/XboxGames/Game/Content/First.exe");
        _ = _bed.WriteFile("X/XboxGames/Game/Content/Second.exe");
        Config("""<Game><ExecutableList><Executable Name="First.exe" /><Executable Name="Second.exe" TargetDeviceFamily="Scarlett" /></ExecutableList></Game>""");

        Assert.AreEqual(first, (await FindAsync(drive)).Single().StoreExecutable);
    }

    [TestMethod]
    [DataRow("""<Game><ExecutableList><Executable Name="Missing.exe" TargetDeviceFamily="PC" /></ExecutableList></Game>""")]
    [DataRow("""<Game><ExecutableList><Executable Name="..\..\Other.exe" TargetDeviceFamily="PC" /></ExecutableList></Game>""")]
    [DataRow("""<Game><ExecutableList><Executable TargetDeviceFamily="PC" /></ExecutableList></Game>""")]
    [DataRow("""<Game><ExecutableList><Executable Name="Game.exe" """)]
    [DataRow("""<!DOCTYPE Game [<!ENTITY e "Game.exe">]><Game><ExecutableList><Executable Name="&e;" /></ExecutableList></Game>""")]
    public async Task A_config_that_names_no_safe_existing_executable_gives_no_pick_but_keeps_the_game(string config)
    {
        DriveRoot drive = _bed.Drive("X");
        _ = _bed.WriteFile("X/XboxGames/Game/Content/Game.exe");
        _ = _bed.WriteFile("X/XboxGames/Other.exe");
        Config(config);

        StoreGame game = (await FindAsync(drive)).Single();

        Assert.IsNull(game.StoreExecutable);
    }

    [TestMethod]
    public async Task A_game_pass_library_on_an_external_drive_is_found()
    {
        DriveRoot drive = _bed.Drive("U", DriveKind.Removable);
        _ = _bed.Folder("U/XboxGames/Starfield/Content");

        StoreBed.AssertNames(await FindAsync(drive), "Starfield");
    }

    [TestMethod]
    [DataRow(DriveKind.Network)]
    [DataRow(DriveKind.Other)]
    public async Task Network_and_other_drives_are_not_searched(DriveKind kind)
    {
        DriveRoot drive = _bed.Drive("U", kind);
        _ = _bed.Folder("U/XboxGames/Game/Content");

        Assert.IsEmpty(await FindAsync(drive));
    }

    [TestMethod]
    public async Task A_drive_that_is_not_ready_does_not_stop_the_others()
    {
        DriveRoot missing = new(_bed.PathOf("NoCard") + Path.DirectorySeparatorChar, DriveKind.Fixed);
        DriveRoot good = _bed.Drive("X");
        _ = _bed.Folder("X/XboxGames/Game/Content");

        StoreBed.AssertNames(await FindAsync(missing, good), "Game");
    }

    [TestMethod]
    public void The_source_says_it_is_xbox() => Assert.AreEqual(GameSource.Xbox, new XboxSource(new FakeDrives()).Source);

    private void Config(string xml) => _ = _bed.WriteFile("X/XboxGames/Game/Content/MicrosoftGame.config", xml);

    private Task<StoreGame[]> FindAsync(params DriveRoot[] drives) => StoreBed.FindAsync(new XboxSource(new FakeDrives(drives)), Ct);
}
