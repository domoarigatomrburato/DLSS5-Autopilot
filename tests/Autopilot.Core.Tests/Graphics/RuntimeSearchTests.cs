using System.Text;
using Autopilot.Core.Graphics;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>A game with no static graphics import loads its renderer by name; the name is in its bytes or beside it.</summary>
[TestClass]
public sealed class RuntimeSearchTests(TestContext testContext) : GraphicsTestBase(testContext)
{
    [TestMethod]
    public async Task An_exe_with_no_graphics_import_and_no_name_in_it_is_unknown()
    {
        ApiDecision decision = await DecideAsync(Game("TheOuterWorlds.exe"));

        Is(decision, GraphicsApi.Unknown, ApiReason.None, "graphics DLL loaded at runtime; assuming DX11/DX12 via dxgi.dll");
        Assert.AreEqual(ApiConfidence.None, decision.Confidence);
    }

    [TestMethod]
    public async Task Call_of_juarez_gunslinger_naming_d3d9_in_utf16_is_directx_9_and_says_it_is_a_guess()
    {
        string exe = Write(Appending(new TestPe(Bits32), Encoding.Unicode, "D3D9.DLL"), "CoJGunslinger.exe");

        ApiDecision decision = await DecideAsync(exe);

        Is(decision, GraphicsApi.DX9, ApiReason.RunTimeName, "loads d3d9.dll at run time", "named in the exe");
        Assert.AreEqual(ApiConfidence.Guessed, decision.Confidence);
    }

    [TestMethod]
    public async Task A_dxgi_name_in_the_exe_outranks_d3d9_as_in_the_static_table() =>
        Is(await DecideAsync(Naming("Game.exe", Bits32, [], "d3d9.dll", "dxgi.dll")), GraphicsApi.Dxgi, ApiReason.RunTimeName, "dxgi.dll");

    [TestMethod]
    [DataRow("xd3d11.dll")]
    [DataRow("libd3d11.dll")]
    [DataRow("my_d3d11.dll")]
    [DataRow("2d3d11.dll")]
    public async Task A_name_inside_a_longer_name_is_not_the_renderer(string longer) =>
        Is(await DecideAsync(Naming("Game.exe", Bits64, [], longer)), GraphicsApi.Unknown, ApiReason.None);

    [TestMethod]
    public async Task A_name_inside_a_longer_utf16_name_is_not_the_renderer() =>
        Is(await DecideAsync(Write(Appending(new TestPe(), Encoding.Unicode, "libd3d11.dll"))), GraphicsApi.Unknown, ApiReason.None);

    [TestMethod]
    [DataRow("/d3d11.dll")]
    [DataRow("C:\\Windows\\d3d11.dll")]
    [DataRow(" d3d11.dll")]
    public async Task A_name_after_a_separator_is_the_renderer(string text) =>
        Is(await DecideAsync(Naming("Game.exe", Bits64, [], text)), GraphicsApi.DX11, ApiReason.RunTimeName);

    [TestMethod]
    public async Task An_engine_dll_beside_the_exe_that_imports_d3d9_is_the_renderer()
    {
        string exe = Game(machine: Bits32);
        Dll(exe, "engine.dll", Bits32, "d3d9.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.SiblingImport, "engine.dll beside the exe imports it");
    }

    [TestMethod]
    public async Task Deadlock_with_qt5gui_importing_d3d11_beside_it_is_dx11()
    {
        string exe = Game("deadlock.exe");
        Dll(exe, "Qt5Gui.dll", Bits64, "d3d11.dll", "opengl32.dll");

        ApiDecision decision = await DecideAsync(exe);

        Is(decision, GraphicsApi.DX11, ApiReason.SiblingImport, "Qt5Gui.dll beside the exe imports it");
        Assert.AreEqual(ApiConfidence.Inferred, decision.Confidence);
    }

    [TestMethod]
    public async Task A_proxy_d3d9_beside_the_exe_is_not_consulted()
    {
        string exe = Game(machine: Bits32);
        Dll(exe, "d3d9.dll", Bits32, "d3d9.dll");
        Dll(exe, "sl.interposer.dll", Bits32, "d3d12.dll");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task A_dll_our_install_record_lists_is_not_sibling_evidence()
    {
        string exe = Naming("Game.exe", Bits64, [], "opengl32.dll");
        Dll(exe, "ourthing.dll", Bits64, "d3d12.dll");
        Record(exe, "ourthing.dll");

        Is(await DecideAsync(exe), GraphicsApi.OpenGL, ApiReason.RunTimeName);
    }

    [TestMethod]
    public async Task A_32_bit_exe_ignores_a_64_bit_dll_beside_it()
    {
        string exe = Game(machine: Bits32);
        Dll(exe, "engine.dll", Bits64, "d3d12.dll");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task A_malformed_dll_beside_the_exe_is_no_evidence()
    {
        string exe = Game();
        Junk(exe, "broken.dll", "MZ" + new string('\xff', 300));
        Dll(exe, "engine.dll", Bits64, "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.SiblingImport, "engine.dll");
    }

    [TestMethod]
    public async Task Only_the_first_60_dlls_by_name_are_read()
    {
        string exe = Game();
        for (int i = 0; i < 60; i++)
        {
            Dll(exe, $"a{i:00}.dll", Bits64, "kernel32.dll");
        }

        Dll(exe, "zz.dll", Bits64, "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task A_d3d8_import_beside_the_exe_is_directx_8_and_d3d9_beside_it_outranks_it()
    {
        string exe = Game(machine: Bits32);
        Dll(exe, "d3ddrv.dll", Bits32, "d3d8.dll");
        Is(await DecideAsync(exe), GraphicsApi.DX8, ApiReason.SiblingImport, "d3ddrv.dll");

        Dll(exe, "d3d9drv.dll", Bits32, "d3d9.dll");
        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.SiblingImport, "d3d9drv.dll");
    }

    [TestMethod]
    public async Task An_exe_naming_only_opengl32_is_opengl()
    {
        ApiDecision decision = await DecideAsync(Naming("HouseParty.exe", Bits32, [], "opengl32.dll"));

        Is(decision, GraphicsApi.OpenGL, ApiReason.RunTimeName, "named in the exe");
    }

    [TestMethod]
    public async Task A_large_dll_naming_d3d11_outranks_the_exes_opengl32_name()
    {
        string exe = Naming("HouseParty.exe", Bits32, [], "opengl32.dll");
        NamingDll(exe, "engine.dll", Bits32, 600_000, "d3d11.dll", "opengl32.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.SiblingString, "named in engine.dll beside the exe");
    }

    [TestMethod]
    [DataRow(400_000)]
    [DataRow(65 * 1024 * 1024)]
    public async Task A_dll_outside_the_string_search_sizes_is_not_searched(int size)
    {
        string exe = Game();
        NamingDll(exe, "engine.dll", Bits64, size, "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task Only_the_6_largest_dlls_have_their_strings_read()
    {
        string exe = Game();
        for (int i = 0; i < 6; i++)
        {
            NamingDll(exe, $"big{i}.dll", Bits64, 700_000, "kernel32.dll");
        }

        NamingDll(exe, "small.dll", Bits64, 600_000, "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task An_exe_that_names_d3d9_is_directx_9_whatever_bink_beside_it_names()
    {
        string exe = Naming("Game.exe", Bits64, [], "d3d9.dll");
        NamingDll(exe, "bink2w64.dll", Bits64, 600_000, "d3d11.dll", "dxgi.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.RunTimeName, "named in the exe");
    }

    [TestMethod]
    public async Task Enshrouded_with_aftermath_beside_it_is_vulkan()
    {
        string exe = Naming("enshrouded.exe", Bits64, [], "vulkan-1.dll");
        NamingDll(exe, "GFSDK_Aftermath_Lib.x64.dll", Bits64, 2 * 1024 * 1024, "d3d12.dll", "dxgi.dll");

        Is(await DecideAsync(exe), GraphicsApi.Vulkan, ApiReason.RunTimeName);
    }

    [TestMethod]
    public async Task A_run_time_d3d9_with_the_games_own_dlss_beside_a_64_bit_exe_is_dx12()
    {
        string exe = Naming("Game64.exe", Bits64, [], "d3d9.dll");
        Junk(exe, "nvngx_dlss.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.DlssFiles, "d3d9.dll", "nvngx_dlss.dll");
    }

    [TestMethod]
    public async Task A_run_time_d3d9_in_a_32_bit_exe_stays_directx_9_whatever_dlss_sits_beside_it()
    {
        string exe = Naming("Game.exe", Bits32, [], "d3d9.dll");
        Dll(exe, "nvngx_dlss.dll", Bits32);

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.RunTimeName);
    }

    [TestMethod]
    public async Task A_name_across_the_edge_of_a_read_block_is_found()
    {
        TestPe pe = new();
        _ = pe.Put(new byte[(4 * 1024 * 1024) - 0x400 - 4]);
        _ = pe.PutAscii("_\0d3d11.dll");

        Is(await DecideAsync(Write(pe)), GraphicsApi.DX11, ApiReason.RunTimeName);
    }

    [TestMethod]
    public async Task An_exe_over_512_mib_is_not_string_searched()
    {
        string exe = Write(Appending(new TestPe { Size = 600L * 1024 * 1024 }, Encoding.Latin1, "d3d9.dll"));

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }

    [TestMethod]
    public async Task A_name_at_the_very_start_of_a_file_counts()
    {
        string exe = Game();
        Junk(exe, "engine.dll", "d3d11.dll" + new string(' ', 600_000));

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.SiblingString, "engine.dll");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_dll_whose_content_is_refused_is_no_evidence()
    {
        string exe = Game();
        Junk(exe, "engine.dll", "d3d11.dll" + new string(' ', 600_000));
        Refuse(Path.Join(Path.GetDirectoryName(exe), "engine.dll"));

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }
}
