using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;
using Autopilot.Core.Tests.Library.Stores;

namespace Autopilot.Core.Tests.Library.Emulators;

/// <summary>The emulator search: where it looks, how deep, and what it leaves alone.</summary>
[TestClass]
public sealed class EmulatorSearchTests(TestContext testContext) : IDisposable
{
    private static readonly HostFolders _noFolders = new(null, null, null, null, null);

    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task An_emulator_is_listed_under_its_name_and_system_with_its_folder_and_executable()
    {
        DriveRoot drive = _bed.Drive("D");
        string exe = _bed.WriteFile("D/Emulators/RetroArch/retroarch.exe");

        StoreGame game = (await FindAsync(drive)).Single();

        Assert.AreEqual(new StoreGame("RetroArch (multi-system)", _bed.PathOf("D/Emulators/RetroArch"), GameSource.Emulator, exe), game);
    }

    [TestMethod]
    public async Task Emulators_with_several_names_are_found_through_the_programs_list_by_any_of_their_names()
    {
        Registered("Eden", "Eden Emulator", "Apps1/Eden", "eden.exe");
        Registered("Lime", "Lime3DS 2119", "Apps2/Lime3DS", "lime3ds.exe");
        Registered("Play", "Play! - PlayStation 2 Emulator", "Apps3/Play", "Play.exe");
        Registered("Sudachi", "sudachi", "Apps4/Sudachi", "sudachi.exe", RegistryRoot.CurrentUser);
        Registered("PPSSPP", "PPSSPP", "Apps5/PPSSPP", "PPSSPPWindows64.exe", view: RegistryView.Registry32);

        StoreBed.AssertNames(
            await FindAsync(), "yuzu / suyu / Eden / Citron (Switch)", "Azahar / Citra / Lime3DS (3DS)", "Play! (PlayStation 2)", "yuzu / suyu / Eden / Citron (Switch)", "PPSSPP (PSP)");
    }

    [TestMethod]
    [DataRow("Playground Tools")]
    [DataRow("PlayStation Plus")]
    [DataRow("Visual Studio Code")]
    public async Task A_program_whose_name_holds_an_emulators_name_only_inside_a_word_is_not_searched(string displayName)
    {
        Registered("Other", displayName, "Apps/Other", "retroarch.exe");

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task A_programs_entry_without_an_install_folder_is_skipped()
    {
        _ = _bed.Registry.Uninstall("Dolphin", displayName: "Dolphin", installLocation: _bed.PathOf("Gone"));
        _ = _bed.Registry.Uninstall("PCSX2", displayName: "PCSX2", installLocation: "relative");
        _ = _bed.Registry.Uninstall("Ryujinx", displayName: "Ryujinx");

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    [DataRow("Program Files")]
    [DataRow("Program Files (x86)")]
    [DataRow("LocalAppData")]
    [DataRow("AppData")]
    [DataRow("Desktop")]
    [DataRow("Downloads")]
    [DataRow("Documents")]
    public async Task The_programs_application_data_and_personal_folders_are_searched(string folder)
    {
        _ = _bed.WriteFile($"{folder}/PCSX2/pcsx2-qt.exe");

        StoreBed.AssertNames(await StoreBed.FindAsync(new EmulatorSource(_bed.Registry, new FakeDrives(), _bed.Folders), Ct), "PCSX2 (PlayStation 2)");
    }

    [TestMethod]
    [DataRow("Emulators")]
    [DataRow("Emulator")]
    [DataRow("Games")]
    [DataRow("Emu")]
    [DataRow("RetroArch")]
    [DataRow("PS2")]
    [DataRow("PS1")]
    [DataRow("Emulation")]
    [DataRow("Roms")]
    [DataRow("Apps")]
    [DataRow("Programs")]
    [DataRow("Oyunlar")]
    [DataRow("Tools")]
    [DataRow("Portable")]
    [DataRow("EmuDeck")]
    [DataRow("Emulators_Portable")]
    [DataRow("LaunchBox")]
    public async Task The_usual_emulator_folders_on_the_system_drive_are_searched(string folder)
    {
        DriveRoot system = SystemDrive();
        _ = _bed.WriteFile($"C/{folder}/Sets/Dolphin/Dolphin.exe");

        StoreBed.AssertNames(await FindAsync(_bed.Folders with { ProgramFiles = _bed.PathOf("C/Program Files") }, system), "Dolphin (GameCube / Wii)");
    }

    [TestMethod]
    public async Task An_emulator_unpacked_at_a_drives_root_is_found_except_on_the_system_drive()
    {
        DriveRoot system = SystemDrive();
        DriveRoot data = _bed.Drive("D");
        _ = _bed.WriteFile("C/PCSX2/pcsx2-qt.exe");
        _ = _bed.WriteFile("D/PCSX2/pcsx2-qt.exe");

        StoreGame game = (await FindAsync(_noFolders with { ProgramFiles = _bed.PathOf("C/Program Files") }, system, data)).Single();

        Assert.AreEqual(_bed.PathOf("D/PCSX2"), game.Folder);
    }

    [TestMethod]
    public async Task Without_knowing_the_system_drive_no_drives_root_is_searched()
    {
        DriveRoot data = _bed.Drive("D");
        _ = _bed.WriteFile("D/PCSX2/pcsx2-qt.exe");

        Assert.IsEmpty(await FindAsync(data));
    }

    [TestMethod]
    [DataRow(DriveKind.Fixed, true)]
    [DataRow(DriveKind.Removable, true)]
    [DataRow(DriveKind.Network, false)]
    [DataRow(DriveKind.Other, false)]
    public async Task Fixed_and_removable_drives_are_searched(DriveKind kind, bool searched)
    {
        DriveRoot drive = _bed.Drive("U", kind);
        _ = _bed.WriteFile("U/Emulators/mGBA/mGBA.exe");

        Assert.HasCount(searched ? 1 : 0, await FindAsync(drive));
    }

    [TestMethod]
    public async Task Every_steam_libraries_games_folder_is_searched()
    {
        string steam = _bed.Folder("Steam/steamapps/common");
        string other = _bed.Folder("E/SteamLibrary/steamapps/common");
        _ = _bed.WriteFile("Steam/steamapps/common/RetroArch/retroarch.exe");
        _ = _bed.WriteFile("E/SteamLibrary/steamapps/common/DuckStation/duckstation-qt-x64.exe");
        string library = Path.GetDirectoryName(Path.GetDirectoryName(other))!.Replace("\\", "\\\\", StringComparison.Ordinal);
        _ = _bed.WriteFile("Steam/steamapps/libraryfolders.vdf", $"\"libraryfolders\"\n{{\n\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{library}\"\n\t}}\n}}\n");
        _ = _bed.Registry.Set(RegistryRoot.CurrentUser, RegistryView.Registry64, @"Software\Valve\Steam", "SteamPath", Path.GetDirectoryName(Path.GetDirectoryName(steam))!);

        StoreBed.AssertNames(await FindAsync(), "RetroArch (multi-system)", "DuckStation (PlayStation 1)");
    }

    [TestMethod]
    public async Task The_root_and_two_levels_of_folders_below_it_are_searched_and_no_deeper()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.WriteFile("D/Emulators/flycast.exe");
        _ = _bed.WriteFile("D/Emulators/One/xemu.exe");
        _ = _bed.WriteFile("D/Emulators/One/Two/Vita3K.exe");
        _ = _bed.WriteFile("D/Emulators/One/Two/Three/rpcs3.exe");

        StoreBed.AssertNames(await FindAsync(drive), "Flycast (Dreamcast)", "xemu (Xbox)", "Vita3K (PS Vita)");
    }

    [TestMethod]
    public async Task Folders_whose_name_starts_with_a_dot_are_skipped()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.WriteFile("D/Emulators/.cache/rpcs3.exe");
        _ = _bed.WriteFile("D/Emulators/Sets/.old/xemu.exe");
        _ = _bed.WriteFile("D/Emulators/Sets/Cemu/Cemu.exe");

        StoreBed.AssertNames(await FindAsync(drive), "Cemu (Wii U)");
    }

    [TestMethod]
    public async Task A_link_below_a_root_is_not_followed()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.WriteFile("Elsewhere/RetroArch/retroarch.exe");
        _ = _bed.Link("D/Emulators/Linked", "Elsewhere/RetroArch");

        Assert.IsEmpty(await FindAsync(drive));
    }

    [TestMethod]
    public async Task An_emulator_reached_through_two_roots_is_listed_once()
    {
        _ = _bed.WriteFile("Documents/RetroArch/retroarch.exe");
        _ = _bed.Link("Desktop", "Documents");

        Assert.HasCount(1, await FindAsync(_noFolders with { Desktop = _bed.PathOf("Desktop"), Documents = _bed.PathOf("Documents") }));
    }

    [TestMethod]
    public async Task One_folder_is_one_emulator_and_other_programs_are_not_emulators()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.WriteFile("D/Emulators/DuckStation/duckstation-qt-x64.exe");
        _ = _bed.WriteFile("D/Emulators/DuckStation/duckstation-nogui-x64.exe");
        _ = _bed.WriteFile("D/Emulators/Other/game.exe");
        _ = _bed.WriteFile("D/Emulators/Other/retroarch.dll");

        Assert.HasCount(1, await FindAsync(drive));
    }

    [TestMethod]
    public async Task A_drive_that_is_not_ready_or_cannot_be_read_does_not_stop_the_others()
    {
        DriveRoot missing = new(_bed.PathOf("NoCard") + Path.DirectorySeparatorChar, DriveKind.Fixed);
        DriveRoot locked = _bed.Drive("Locked");
        _ = _bed.WriteFile("Locked/Emulators/Hidden/rpcs3.exe");
        _bed.Deny("Locked/Emulators");
        DriveRoot good = _bed.Drive("E");
        _ = _bed.WriteFile("E/Emulators/Found/rpcs3.exe");

        StoreGame game = (await FindAsync(missing, locked, good)).Single();

        Assert.AreEqual(_bed.PathOf("E/Emulators/Found"), game.Folder);
    }

    [TestMethod]
    public async Task A_root_full_of_folders_is_searched_up_to_two_thousand_folders()
    {
        DriveRoot drive = _bed.Drive("D");
        for (int i = 0; i < 2000; i++)
        {
            _ = _bed.Folder($"D/Roms/{i:D4}");
        }

        _ = _bed.WriteFile("D/Roms/0000/xemu.exe");
        _ = _bed.WriteFile("D/Roms/1999/rpcs3.exe");

        StoreBed.AssertNames(await FindAsync(drive), "xemu (Xbox)");
    }

    [TestMethod]
    public async Task A_cancelled_search_stops_with_cancellation()
    {
        DriveRoot drive = _bed.Drive("D");
        _ = _bed.WriteFile("D/Emulators/Cemu/Cemu.exe");

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => new EmulatorSource(_bed.Registry, new FakeDrives(drive), _noFolders).FindAsync(new CancellationToken(canceled: true)));
    }

    [TestMethod]
    public void The_source_says_it_is_the_emulator_search() =>
        Assert.AreEqual(GameSource.Emulator, new EmulatorSource(_bed.Registry, new FakeDrives(), _noFolders).Source);

    private void Registered(string key, string displayName, string folder, string executable, RegistryRoot root = RegistryRoot.LocalMachine, RegistryView view = RegistryView.Registry64)
    {
        _ = _bed.WriteFile($"{folder}/{executable}");
        _ = _bed.Registry.Uninstall(key, root, view, displayName: displayName, installLocation: _bed.PathOf(folder));
    }

    // The system drive is the one holding Program Files.
    private DriveRoot SystemDrive()
    {
        _ = _bed.Folder("C/Program Files");
        return _bed.Drive("C");
    }

    private Task<StoreGame[]> FindAsync(params DriveRoot[] drives) => FindAsync(_noFolders, drives);

    private Task<StoreGame[]> FindAsync(HostFolders folders, params DriveRoot[] drives) =>
        StoreBed.FindAsync(new EmulatorSource(_bed.Registry, new FakeDrives(drives), folders), Ct);
}
