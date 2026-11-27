using Autopilot.Core.Graphics;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Tests.Library.Scanning;

namespace Autopilot.Core.Tests.Library.Emulators;

/// <summary>Which games are known emulators: by the executable's name, and for generic names only when the emulator search or the person found them.</summary>
[TestClass]
public sealed class EmulatorProfileTests(TestContext testContext) : IDisposable
{
    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    [DataRow("duckstation-qt-x64.exe", "DuckStation")]
    [DataRow("duckstation-nogui-x64.exe", "DuckStation")]
    [DataRow("duckstation.exe", "DuckStation")]
    [DataRow("pcsx2-qt.exe", "PCSX2")]
    [DataRow("pcsx2x64.exe", "PCSX2")]
    [DataRow("pcsx2x64-avx2.exe", "PCSX2")]
    [DataRow("pcsx2.exe", "PCSX2")]
    [DataRow("DolphinQt.exe", "Dolphin")]
    [DataRow("PPSSPPWindows64.exe", "PPSSPP")]
    [DataRow("PPSSPPWindows.exe", "PPSSPP")]
    [DataRow("xenia_canary.exe", "Xenia")]
    [DataRow("rpcs3.exe", "RPCS3")]
    [DataRow("Ryujinx.exe", "Ryujinx")]
    [DataRow("Ryujinx.Ava.exe", "Ryujinx")]
    [DataRow("Ryujinx.Headless.SDL2.exe", "Ryujinx")]
    [DataRow("yuzu.exe", "yuzu / suyu / Eden / Citron")]
    [DataRow("suyu.exe", "yuzu / suyu / Eden / Citron")]
    [DataRow("eden.exe", "yuzu / suyu / Eden / Citron")]
    [DataRow("citron.exe", "yuzu / suyu / Eden / Citron")]
    [DataRow("sudachi.exe", "yuzu / suyu / Eden / Citron")]
    [DataRow("shadPS4.exe", "shadPS4")]
    [DataRow("azahar.exe", "Azahar / Citra / Lime3DS")]
    [DataRow("citra.exe", "Azahar / Citra / Lime3DS")]
    [DataRow("citra-qt.exe", "Azahar / Citra / Lime3DS")]
    [DataRow("lime3ds.exe", "Azahar / Citra / Lime3DS")]
    [DataRow("melonDS.exe", "melonDS")]
    [DataRow("flycast.exe", "Flycast")]
    [DataRow("xemu.exe", "xemu")]
    [DataRow("Vita3K.exe", "Vita3K")]
    [DataRow("retroarch.exe", "RetroArch")]
    [DataRow("mGBA.exe", "mGBA")]
    [DataRow("snes9x-x64.exe", "Snes9x")]
    [DataRow("snes9x.exe", "Snes9x")]
    [DataRow("RETROARCH.EXE", "RetroArch")]
    public async Task A_store_game_whose_executable_is_a_known_emulator_is_that_emulator_and_keeps_the_stores_name(string executable, string emulator)
    {
        _ = _bed.Exe($"Emu/{executable}");

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Emu on Steam", "Emu"), Ct);

        Assert.AreEqual((emulator, "Emu on Steam"), (entry.Emulator?.Name, entry.Name));
    }

    [TestMethod]
    [DataRow("Play.exe")]
    [DataRow("Dolphin.exe")]
    [DataRow("Cemu.exe")]
    [DataRow("xenia.exe")]
    [DataRow("PLAY.EXE")]
    public async Task A_store_game_whose_executable_has_a_name_other_programs_use_stays_a_game(string executable)
    {
        _ = _bed.Exe($"Some Game/{executable}", imports: "d3d11.dll");

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Some Game", "Some Game"), Ct);

        Assert.IsNull(entry.Emulator);
        Assert.AreEqual(("Some Game", GraphicsApi.DX11), (entry.Name, entry.Api));
    }

    [TestMethod]
    [DataRow("Play.exe", "Play!")]
    [DataRow("Dolphin.exe", "Dolphin")]
    [DataRow("Cemu.exe", "Cemu")]
    [DataRow("xenia.exe", "Xenia")]
    public async Task A_name_other_programs_use_is_the_emulator_when_the_emulator_search_found_it(string executable, string emulator)
    {
        string exe = _bed.Exe($"Emu/{executable}");

        GameEntry entry = await _bed.ReadAsync(_bed.Game($"{emulator} (system)", "Emu", GameSource.Emulator, $"Emu/{executable}"), Ct);

        Assert.AreEqual((emulator, exe), (entry.Emulator?.Name, entry.Executable));
    }

    [TestMethod]
    [DataRow("Play.exe", "Play! (PlayStation 2)")]
    [DataRow("Cemu.exe", "Cemu (Wii U)")]
    [DataRow("duckstation-qt-x64.exe", "DuckStation (PlayStation 1)")]
    [DataRow("Dolphin.exe", "Dolphin (GameCube / Wii)")]
    public async Task An_emulator_added_by_hand_is_named_after_the_emulator_and_its_system(string executable, string name)
    {
        string exe = _bed.Exe($"Tools/bin/{executable}");

        GameAdded added = Assert.IsInstanceOfType<GameAdded>(await _bed.AddAsync(exe, Ct));

        Assert.AreEqual(name, added.Entry.Name);
        Assert.IsNotNull(added.Entry.Emulator);
    }

    [TestMethod]
    public async Task A_remembered_folder_holding_an_emulator_comes_back_named_after_it()
    {
        _ = _bed.Exe("Tools/xenia/xenia.exe");

        LibraryScan scan = await Scans.ScanAsync(_bed.Full() with { HandAddedFolders = [_bed.PathOf("Tools/xenia")] }, Ct);

        Assert.AreEqual("Xenia (Xbox 360) (Manual)", Scans.Listed(scan));
    }

    [TestMethod]
    public async Task A_game_added_by_hand_that_is_no_emulator_keeps_its_folders_name()
    {
        string exe = _bed.Exe("World War Z/bin/WWZ.exe");

        GameAdded added = Assert.IsInstanceOfType<GameAdded>(await _bed.AddAsync(exe, Ct));

        Assert.AreEqual("World War Z", added.Entry.Name);
        Assert.IsNull(added.Entry.Emulator);
    }

    [TestMethod]
    public async Task An_emulator_found_by_the_emulator_search_keeps_the_searches_name()
    {
        _ = _bed.Exe("Emu/Play.exe");

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Play! (PlayStation 2)", "Emu", GameSource.Emulator, "Emu/Play.exe"), Ct);

        Assert.AreEqual("Play! (PlayStation 2)", entry.Name);
    }

    [TestMethod]
    public async Task An_emulator_whose_executable_cannot_be_read_is_still_that_emulator()
    {
        _ = _bed.NotAnExe("Emu/retroarch.exe");

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Emu", "Emu", GameSource.Emulator, "Emu/retroarch.exe"), Ct);

        Assert.AreEqual("RetroArch", entry.Emulator?.Name);
        Assert.IsNotNull(entry.Error);
    }

    [TestMethod]
    [DataRow("duckstation.exe", "DuckStation (PlayStation 1)", "Settings > Graphics > Renderer = Direct3D 11 (or 12)", "Raise the internal resolution scale to 2x-4x so DLAA has detail to work with.", true)]
    [DataRow("pcsx2-qt.exe", "PCSX2 (PlayStation 2)", "Settings > Graphics > Renderer = Direct3D 11 (or 12)", "An upscale multiplier of 2x or more is recommended.", true)]
    [DataRow("Cemu.exe", "Cemu (Wii U)", "Cemu offers Vulkan or OpenGL - set Vulkan (beta path)", "Vulkan goes through ReShade's layer registration; OpenGL installs as opengl32.dll and is the least reliable.", false)]
    [DataRow("melonDS.exe", "melonDS (DS)", "OpenGL only - the least reliable path", null, false)]
    [DataRow("snes9x.exe", "Snes9x (SNES)", "Set the output method to Direct3D", null, true)]
    public async Task The_person_reads_how_to_set_each_emulator_up(string executable, string title, string hint, string? note, bool direct3D)
    {
        _ = _bed.Exe($"Emu/{executable}");

        EmulatorProfile emulator = (await _bed.ReadAsync(_bed.Game("Emu", "Emu", GameSource.Emulator, $"Emu/{executable}"), Ct)).Emulator!;

        Assert.AreEqual((title, hint, note, direct3D), (emulator.Title, emulator.Hint, emulator.Note, emulator.PresentsThroughDirect3D));
        CollectionAssert.Contains(emulator.Executables.ToArray(), executable);
    }

    [TestMethod]
    public async Task A_game_whose_executable_is_no_emulator_has_none()
    {
        _ = _bed.Exe("Game/Game.exe");

        Assert.IsNull((await _bed.ReadAsync(_bed.Game("Game", "Game", GameSource.Emulator, "Game/Game.exe"), Ct)).Emulator);
    }
}
