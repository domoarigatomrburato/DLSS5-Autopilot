using System.Text;
using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;
using Autopilot.Core.Tests.Library.Scanning;

namespace Autopilot.Core.Tests.Library.Emulators;

/// <summary>The render backend an emulator is set to, read from its own config for the game's status; nothing is written.</summary>
[TestClass]
public sealed class EmulatorBackendTests(TestContext testContext) : IDisposable
{
    private const string Unknown = "unknown";

    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    private HostFolders Folders => new(null, _bed.PathOf("LocalAppData"), _bed.PathOf("AppData"), null, null, Documents: _bed.PathOf("Documents"));

    public void Dispose() => _bed.Dispose();

    // The old tests' configs: the backend on a value that is not DXGI, among keys that must not be read.
    [TestMethod]
    [DataRow("duckstation-qt-x64.exe", "", "settings.ini", "[Main]\nSettingsVersion = 3\n\n[GPU]\nRenderer = Vulkan\nResolutionScale = 3\n\n[Audio]\nBackend = Cubeb\n")]
    [DataRow("pcsx2-qt.exe", "portable.ini", "inis/PCSX2.ini", "[UI]\r\nMainWindowGeometry = x\r\n\r\n[EmuCore/GS]\r\nVsyncEnable = 0\r\nRenderer = 14\r\nupscale_multiplier = 2\r\n\r\n[EmuCore]\r\nRenderer = 99\r\n")]
    [DataRow("Dolphin.exe", "portable.txt", "User/Config/Dolphin.ini", "[General]\nISOPath0 = D:/wii\n[Core]\nGFXBackend = Vulkan\nCPUThread = True\n")]
    [DataRow("PPSSPPWindows64.exe", "", "memstick/PSP/SYSTEM/ppsspp.ini", "[General]\nLanguage = en_US\n[Graphics]\nFailedGraphicsBackends = \nGraphicsBackend = 3 (VULKAN)\nInternalResolution = 3\n")]
    [DataRow("xenia_canary.exe", "portable.txt", "xenia-canary.config.toml", "[APU]\napu = \"any\"\n\n[GPU]\ndraw_resolution_scale_x = 2\ngpu = \"vulkan\"\nvsync = true\n")]
    [DataRow("retroarch.exe", "", "retroarch.cfg", "audio_driver = \"xaudio\"\nvideo_driver = \"vulkan\"\nvideo_fullscreen = \"false\"\n")]
    [DataRow("rpcs3.exe", "", "config.yml", "Core:\n  PPU Decoder: Recompiler (LLVM)\nVideo:\n  Renderer: Vulkan\n  Resolution: 1280x720\n")]
    [DataRow("Cemu.exe", "", "settings.xml", "<?xml version=\"1.0\"?>\n<content>\n  <Graphic>\n    <api>1</api>\n    <device></device>\n  </Graphic>\n</content>\n")]
    public async Task Each_emulators_config_says_it_runs_on_Vulkan(string executable, string marker, string config, string text)
    {
        string exe = Emulator(executable, marker);
        string file = _bed.Write($"Emu/{config}", text);

        EmulatorBackend backend = await StatusAsync(exe);

        Assert.AreEqual(new EmulatorBackend("Vulkan", false, file), backend);
    }

    [TestMethod]
    [DataRow("duckstation-qt-x64.exe", "settings.ini", "[GPU]\nRenderer = D3D11\n", "D3D11")]
    [DataRow("duckstation-qt-x64.exe", "settings.ini", "[GPU]\nRenderer = D3D12\n", "D3D12")]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 3\n", "D3D11")]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 15\n", "D3D12")]
    [DataRow("Dolphin.exe", "User/Config/Dolphin.ini", "[Core]\nGFXBackend = D3D\n", "D3D11")]
    [DataRow("Dolphin.exe", "User/Config/Dolphin.ini", "[Core]\nGFXBackend = D3D12\n", "D3D12")]
    [DataRow("PPSSPPWindows64.exe", "memstick/PSP/SYSTEM/ppsspp.ini", "[Graphics]\nGraphicsBackend = 2 (DIRECT3D11)\n", "D3D11")]
    [DataRow("PPSSPPWindows64.exe", "memstick/PSP/SYSTEM/ppsspp.ini", "[Graphics]\nGraphicsBackend = 2\n", "D3D11")]
    [DataRow("PPSSPPWindows64.exe", "memstick/PSP/SYSTEM/ppsspp.ini", "[Graphics]\nGraphicsBackend = DIRECT3D11\n", "DIRECT3D11")]
    [DataRow("xenia_canary.exe", "xenia-canary.config.toml", "[GPU]\ngpu = \"d3d12\"\n", "D3D12")]
    [DataRow("retroarch.exe", "retroarch.cfg", "video_driver = \"d3d11\"\n", "D3D11")]
    [DataRow("retroarch.exe", "retroarch.cfg", "video_driver = \"d3d12\"\n", "D3D12")]
    public async Task A_config_already_on_Direct3D_11_or_12_says_so(string executable, string config, string text, string name)
    {
        string exe = Emulator(executable, "portable.txt", "portable.ini");
        _ = _bed.Write($"Emu/{config}", text);

        EmulatorBackend backend = await StatusAsync(exe);

        Assert.AreEqual((name, true), (backend.Name, backend.AlreadyDxgi));
    }

    [TestMethod]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = -1\n", "Automatic")]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 12\n", "OpenGL")]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 13\n", "Software")]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 11\n", "Null")]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 99\n", "99")]
    [DataRow("PPSSPPWindows64.exe", "memstick/PSP/SYSTEM/ppsspp.ini", "[Graphics]\nGraphicsBackend = 0 (OPENGL)\n", "OpenGL")]
    [DataRow("Dolphin.exe", "User/Config/Dolphin.ini", "[Core]\nGFXBackend = OGL\n", "OpenGL")]
    [DataRow("Dolphin.exe", "User/Config/Dolphin.ini", "[Core]\nGFXBackend = Software Renderer\n", "Software")]
    [DataRow("xenia_canary.exe", "xenia-canary.config.toml", "[GPU]\ngpu = \"any\"\n", "Automatic")]
    [DataRow("retroarch.exe", "retroarch.cfg", "video_driver = \"glcore\"\n", "OpenGL")]
    [DataRow("retroarch.exe", "retroarch.cfg", "video_driver = \"d3d9\"\n", "D3D9")]
    [DataRow("retroarch.exe", "retroarch.cfg", "video_driver = \"GL\"\n", "OpenGL")]
    [DataRow("rpcs3.exe", "config.yml", "Video:\n  Renderer: OpenGL\n", "OpenGL")]
    [DataRow("Cemu.exe", "settings.xml", "<content><Graphic><api>0</api></Graphic></content>", "OpenGL")]
    public async Task Every_other_backend_reads_in_the_status_words(string executable, string config, string text, string name)
    {
        string exe = Emulator(executable, "portable.txt", "portable.ini");
        _ = _bed.Write($"Emu/{config}", text);

        EmulatorBackend backend = await StatusAsync(exe);

        Assert.AreEqual((name, false), (backend.Name, backend.AlreadyDxgi));
    }

    [TestMethod]
    public async Task A_config_written_in_UTF_16_with_a_BOM_is_read()
    {
        string exe = Emulator("pcsx2-qt.exe", "portable.ini");
        string file = _bed.PathOf("Emu/inis/PCSX2.ini");
        TestFiles.Plant(file, [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("[EmuCore/GS]\r\nRenderer = 15\r\n")]);

        Assert.AreEqual(new EmulatorBackend("D3D12", true, file), await StatusAsync(exe));
    }

    [TestMethod]
    public async Task A_config_written_in_UTF_8_with_a_BOM_and_CRLF_is_read()
    {
        string exe = Emulator("duckstation-qt-x64.exe");
        TestFiles.Plant(_bed.PathOf("Emu/settings.ini"), [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("[GPU]\r\nRenderer = D3D11\r\n")]);

        Assert.AreEqual("D3D11", (await StatusAsync(exe)).Name);
    }

    [TestMethod]
    [DataRow("duckstation-qt-x64.exe", "settings.ini", "[GPU]\n; Renderer = D3D12\nRenderer = Vulkan ; set by hand\n")]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "[EmuCore/GS]\n# Renderer = 15\nRenderer = 14 # Vulkan\n")]
    [DataRow("xenia_canary.exe", "xenia-canary.config.toml", "[GPU]\n# gpu = \"d3d12\"\ngpu = \"vulkan\"  # was \"any\"\n")]
    [DataRow("retroarch.exe", "retroarch.cfg", "# video_driver = \"d3d11\"\nvideo_driver = \"vulkan\"\n")]
    [DataRow("rpcs3.exe", "config.yml", "# Video:\n#   Renderer: OpenGL\nAudio:\n  Renderer: Cubeb\nVideo:\n  Renderer: \"Vulkan\" # chosen\n")]
    [DataRow("Cemu.exe", "settings.xml", "<content>\n  <!-- <Graphic><api>0</api></Graphic> -->\n  <api>0</api>\n  <Graphic>\n    <api> 1 </api>\n  </Graphic>\n</content>\n")]
    public async Task Comments_and_other_sections_do_not_count(string executable, string config, string text)
    {
        string exe = Emulator(executable, "portable.txt", "portable.ini");
        _ = _bed.Write($"Emu/{config}", text);

        Assert.AreEqual("Vulkan", (await StatusAsync(exe)).Name);
    }

    [TestMethod]
    [DataRow("pcsx2-qt.exe", "inis/PCSX2.ini", "Documents/PCSX2/inis/PCSX2.ini")]
    [DataRow("Dolphin.exe", "User/Config/Dolphin.ini", "Documents/Dolphin Emulator/Config/Dolphin.ini")]
    [DataRow("xenia.exe", "xenia.config.toml", "Documents/Xenia/xenia.config.toml")]
    public async Task A_config_beside_the_executable_counts_only_when_the_emulator_runs_portable(string executable, string beside, string user)
    {
        string exe = Emulator(executable);
        _ = _bed.Write($"Emu/{beside}", "[EmuCore/GS]\nRenderer = 15\n[Core]\nGFXBackend = D3D12\n[GPU]\ngpu = \"d3d12\"\n");
        string file = _bed.Write(user, "[EmuCore/GS]\nRenderer = 14\n[Core]\nGFXBackend = Vulkan\n[GPU]\ngpu = \"vulkan\"\n");

        Assert.AreEqual(new EmulatorBackend("Vulkan", false, file), await StatusAsync(exe));
    }

    [TestMethod]
    [DataRow("portable.txt")]
    [DataRow("portable.ini")]
    public async Task Either_PCSX2_marker_makes_it_portable(string marker)
    {
        string exe = Emulator("pcsx2-qt.exe", marker);
        string file = _bed.Write("Emu/inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 15\n");
        _ = _bed.Write("Documents/PCSX2/inis/PCSX2.ini", "[EmuCore/GS]\nRenderer = 14\n");

        Assert.AreEqual(file, (await StatusAsync(exe)).ConfigFile);
    }

    [TestMethod]
    [DataRow("duckstation-qt-x64.exe", "LocalAppData/DuckStation/settings.ini", "[GPU]\nRenderer = Vulkan\n")]
    [DataRow("Dolphin.exe", "AppData/Dolphin Emulator/Config/Dolphin.ini", "[Core]\nGFXBackend = Vulkan\n")]
    [DataRow("retroarch.exe", "AppData/RetroArch/retroarch.cfg", "video_driver = \"vulkan\"\n")]
    [DataRow("Cemu.exe", "AppData/Cemu/settings.xml", "<content><Graphic><api>1</api></Graphic></content>")]
    [DataRow("xenia.exe", "Documents/Xenia/xenia-canary.config.toml", "[GPU]\ngpu = \"vulkan\"\n")]
    public async Task The_application_data_folders_are_read_when_nothing_comes_first(string executable, string config, string text)
    {
        string exe = Emulator(executable, "portable.txt");
        string file = _bed.Write(config, text);

        Assert.AreEqual(new EmulatorBackend("Vulkan", false, file), await StatusAsync(exe));
    }

    [TestMethod]
    public async Task DuckStation_portable_without_its_settings_reads_the_documents_one_before_the_application_data()
    {
        string exe = Emulator("duckstation-qt-x64.exe", "portable.txt");
        string file = _bed.Write("Documents/DuckStation/settings.ini", "[GPU]\nRenderer = Vulkan\n");
        _ = _bed.Write("LocalAppData/DuckStation/settings.ini", "[GPU]\nRenderer = D3D12\n");

        Assert.AreEqual(file, (await StatusAsync(exe)).ConfigFile);
    }

    [TestMethod]
    [DataRow("xenia_canary.exe", "xenia-canary.config.toml")]
    [DataRow("xenia.exe", "xenia.config.toml")]
    public async Task Each_Xenia_build_reads_its_own_file_first(string executable, string own)
    {
        string exe = Emulator(executable, "portable.txt");
        _ = _bed.Write("Emu/xenia-canary.config.toml", "[GPU]\ngpu = \"vulkan\"\n");
        _ = _bed.Write("Emu/xenia.config.toml", "[GPU]\ngpu = \"vulkan\"\n");

        Assert.AreEqual(_bed.PathOf($"Emu/{own}"), (await StatusAsync(exe)).ConfigFile);
    }

    [TestMethod]
    public async Task PPSSPP_without_installed_txt_reads_the_memory_stick_beside_it_first()
    {
        string exe = Emulator("PPSSPPWindows64.exe");
        string beside = Stick("Emu/memstick");
        _ = Stick("Documents/PPSSPP");

        Assert.AreEqual(beside, (await StatusAsync(exe)).ConfigFile);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\r\n")]
    [DataRow("../Elsewhere")]
    [DataRow("Sub/../../Elsewhere")]
    [DataRow("outside")]
    public async Task PPSSPP_installed_with_an_empty_or_escaping_path_reads_the_documents_stick_first(string named)
    {
        string exe = Emulator("PPSSPPWindows64.exe");
        _ = Stick("Emu/memstick");
        _ = Stick("Elsewhere");
        string documents = Stick("Documents/PPSSPP");
        _ = _bed.Write("Emu/installed.txt", named == "outside" ? _bed.PathOf("Elsewhere") : named);

        Assert.AreEqual(documents, (await StatusAsync(exe)).ConfigFile);
    }

    [TestMethod]
    [DataRow("Stick")]
    [DataRow("inside")]
    public async Task PPSSPP_installed_with_a_path_below_it_reads_that_stick_first(string named)
    {
        string exe = Emulator("PPSSPPWindows64.exe");
        string own = Stick("Emu/Stick");
        _ = Stick("Documents/PPSSPP");
        _ = _bed.Write("Emu/installed.txt", (named == "inside" ? _bed.PathOf("Emu/Stick") : named) + "\r\nignored\r\n");

        Assert.AreEqual(own, (await StatusAsync(exe)).ConfigFile);
    }

    [TestMethod]
    public async Task No_config_file_reads_unknown()
    {
        string exe = Emulator("pcsx2-qt.exe", "portable.ini");

        Assert.AreEqual(new EmulatorBackend(Unknown, false, null), await StatusAsync(exe));
    }

    [TestMethod]
    [DataRow("duckstation-qt-x64.exe", "settings.ini", "[Audio]\nRenderer = D3D12\n")]
    [DataRow("duckstation-qt-x64.exe", "settings.ini", "[GPU]\nRenderer =\n")]
    [DataRow("retroarch.exe", "retroarch.cfg", "audio_driver = \"xaudio\"\n")]
    [DataRow("rpcs3.exe", "config.yml", "Audio:\n  Renderer: Cubeb\n")]
    [DataRow("Cemu.exe", "settings.xml", "<content><api>1</api></content>")]
    [DataRow("Cemu.exe", "settings.xml", "<content><Graphic><api>1</api>")]
    [DataRow("Cemu.exe", "settings.xml", "<?xml version=\"1.0\"?>\n<!DOCTYPE content [<!ENTITY api \"1\">]>\n<content><Graphic><api>&api;</api></Graphic></content>")]
    public async Task A_config_without_the_setting_reads_unknown_and_names_the_file(string executable, string config, string text)
    {
        string exe = Emulator(executable);
        string file = _bed.Write($"Emu/{config}", text);

        Assert.AreEqual(new EmulatorBackend(Unknown, false, file), await StatusAsync(exe));
    }

    [TestMethod]
    public async Task A_config_too_large_to_be_one_is_not_read()
    {
        string exe = Emulator("retroarch.exe");
        string file = _bed.Write("Emu/retroarch.cfg", "video_driver = \"vulkan\"\n" + new string('#', 1024 * 1024));

        Assert.AreEqual(new EmulatorBackend(Unknown, false, file), await StatusAsync(exe));
    }

    [TestMethod]
    [DataRow("Ryujinx.exe")]
    [DataRow("eden.exe")]
    [DataRow("melonDS.exe")]
    public async Task An_emulator_whose_config_is_not_read_reads_unknown(string executable)
    {
        string exe = Emulator(executable);
        _ = _bed.Write("Emu/settings.ini", "[GPU]\nRenderer = Vulkan\n");

        Assert.AreEqual(new EmulatorBackend(Unknown, false, null), await StatusAsync(exe));
    }

    // The emulator's executable, and its marker files beside it.
    private string Emulator(string executable, params string[] markers)
    {
        foreach (string marker in markers.Where(marker => marker.Length > 0))
        {
            _ = _bed.Write($"Emu/{marker}", "");
        }

        return _bed.NotAnExe($"Emu/{executable}");
    }

    // A memory stick folder holding PPSSPP's config; returns the config's path.
    private string Stick(string folder) => _bed.Write($"{folder}/PSP/SYSTEM/ppsspp.ini", "[Graphics]\nGraphicsBackend = 3 (VULKAN)\n");

    // The emulator as the scan reads it, from its executable in Emu.
    private async Task<EmulatorBackend> StatusAsync(string executable)
    {
        EmulatorProfile emulator = (await _bed.ReadAsync(_bed.Game("Emu", "Emu", GameSource.Emulator, $"Emu/{Path.GetFileName(executable)}"), Ct)).Emulator!;
        return await EmulatorConfig.ReadBackendAsync(emulator, executable, Folders, Ct);
    }
}
