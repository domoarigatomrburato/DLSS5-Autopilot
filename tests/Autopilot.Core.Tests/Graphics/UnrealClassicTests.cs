using System.Text;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>Unreal Engine 1 and 2: the executable in <c>System</c> loads the render device its ini names.</summary>
[TestClass]
public sealed class UnrealClassicTests(TestContext testContext) : GraphicsTestBase(testContext)
{
    private const string Ini = "[URL]\r\nPort=7777\r\n\r\n[Engine.Engine]\r\nRenderDevice = D3DDrv.D3DRenderDevice ; set by the game\r\n";

    [TestMethod]
    public async Task Postal_2_with_no_ini_and_both_modules_is_directx_9()
    {
        string exe = Postal2(d3d9Drv: true);

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.UnrealClassic, "Unreal Engine 1/2", "D3D9Drv", "d3d9.dll");
    }

    [TestMethod]
    public async Task Postal_2_with_only_d3ddrv_importing_d3d8_is_directx_8()
    {
        ApiDecision decision = await DecideAsync(Postal2(d3d9Drv: false));

        Is(decision, GraphicsApi.DX8, ApiReason.UnrealClassic, "D3DDrv.dll imports d3d8.dll");
        Assert.AreEqual(ApiConfidence.Guessed, decision.Confidence);
    }

    [TestMethod]
    public async Task The_games_own_ini_decides_over_the_modules_present()
    {
        string exe = Postal2(d3d9Drv: true);
        Junk(exe, "Postal2.ini", Ini);

        Is(await DecideAsync(exe), GraphicsApi.DX8, ApiReason.UnrealClassic, "D3DDrv as the RenderDevice in Postal2.ini");
    }

    [TestMethod]
    [DataRow("utf-16-bom")]
    [DataRow("utf-16")]
    [DataRow("utf-16-be")]
    [DataRow("utf-8-bom")]
    [DataRow("latin-1")]
    public async Task An_ini_is_read_in_each_encoding_with_a_quoted_value(string encoding)
    {
        string exe = Postal2(d3d9Drv: true);
        TestFiles.Plant(Path.Join(Path.GetDirectoryName(exe), "Postal2.ini"), Encoded(encoding, "; ü\r\n[Engine.Engine]\r\nRenderDevice=\"OpenGLDrv.OpenGLRenderDevice\"\r\n"));

        Is(await DecideAsync(exe), GraphicsApi.OpenGL, ApiReason.UnrealClassic, "OpenGLDrv is the OpenGL renderer");
    }

    [TestMethod]
    public async Task Default_ini_stands_in_when_the_game_has_not_written_its_own()
    {
        string exe = Postal2(d3d9Drv: false);
        TestFiles.Plant(Path.Join(Path.GetDirectoryName(exe), "Default.ini"), Encoded("utf-8-bom", "[engine.engine]\nRenderDevice=D3D9Drv.D3D9RenderDevice\nRenderDevice=D3DDrv.D3DRenderDevice\n"));

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.UnrealClassic, "Default.ini", "D3D9Drv is the DX9 renderer");
    }

    [TestMethod]
    public async Task A_device_outside_the_engine_section_or_commented_out_is_not_read()
    {
        string exe = Postal2(d3d9Drv: true);
        Junk(exe, "Postal2.ini", "[Other]\nRenderDevice=OpenGLDrv.X\n[Engine.Engine]\n;RenderDevice=OpenGLDrv.X\n");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.UnrealClassic, "D3D9Drv.dll beside the exe");
    }

    [TestMethod]
    public async Task An_unreal_1_direct3d_7_renderer_is_unknown_and_says_what_to_switch_to()
    {
        string exe = Postal2(d3d9Drv: false, d3dDrvImport: "ddraw.dll");
        Junk(exe, "Postal2.ini", "[Engine.Engine]\r\nGameRenderDevice=D3DDrv.D3DRenderDevice\r\nRenderDevice=GlideDrv.GlideRenderDevice\r\n");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None, "Direct3D 7", "GameRenderDevice", "OpenGL");
    }

    [TestMethod]
    public async Task An_ini_naming_a_module_by_a_path_is_never_opened_as_given()
    {
        string exe = Postal2(d3d9Drv: false);
        Dll(exe, Path.Join("..", "Evil.dll"), Bits32, "d3d12.dll");
        Junk(exe, "Postal2.ini", "[Engine.Engine]\nRenderDevice=../Evil.X\n");

        Is(await DecideAsync(exe), GraphicsApi.DX8, ApiReason.UnrealClassic, "D3DDrv.dll imports d3d8.dll");
    }

    [TestMethod]
    public async Task A_system_folder_without_core_and_engine_is_not_unreal()
    {
        string exe = Postal2(d3d9Drv: true);
        File.Delete(Path.Join(Path.GetDirectoryName(exe), "Core.dll"));

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.SiblingImport, "D3D9Drv.dll beside the exe imports it");
    }

    [TestMethod]
    public async Task An_ini_over_4_mib_is_not_read()
    {
        string exe = Postal2(d3d9Drv: true);
        Junk(exe, "Postal2.ini", "[Engine.Engine]\nRenderDevice=D3DDrv.X\n" + new string(';', 4 * 1024 * 1024));

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.UnrealClassic);
    }

    private string Postal2(bool d3d9Drv, string d3dDrvImport = "d3d8.dll")
    {
        string exe = Game(Path.Join("POSTAL2", "System", "Postal2.exe"), Bits32);
        Junk(exe, "Core.dll");
        Junk(exe, "Engine.dll");
        Dll(exe, "D3DDrv.dll", Bits32, d3dDrvImport, "kernel32.dll");
        if (d3d9Drv)
        {
            Dll(exe, "D3D9Drv.dll", Bits32, "d3d9.dll", "kernel32.dll");
        }

        return exe;
    }

    private static byte[] Encoded(string encoding, string text) => encoding switch
    {
        "utf-16-bom" => [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)],
        "utf-16" => Encoding.Unicode.GetBytes(text),
        "utf-16-be" => [.. Encoding.BigEndianUnicode.GetPreamble(), .. Encoding.BigEndianUnicode.GetBytes(text)],
        "utf-8-bom" => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)],
        _ => Encoding.Latin1.GetBytes(text),
    };

    [TestMethod]
    public async Task A_module_importing_no_renderer_is_read_by_its_name()
    {
        string exe = Postal2(d3d9Drv: false);
        Dll(exe, "D3D9Drv.dll", Bits32, "kernel32.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.UnrealClassic, "D3D9Drv is the DX9 renderer");
    }

    [TestMethod]
    public async Task A_layout_naming_no_device_it_knows_falls_to_the_run_time_search()
    {
        string exe = Postal2(d3d9Drv: false);
        File.Delete(Path.Join(Path.GetDirectoryName(exe), "D3DDrv.dll"));
        Junk(exe, "Postal2.ini", "[Engine.Engine]\nRenderDevice=GlideDrv.GlideRenderDevice\n");

        Is(await DecideAsync(exe), GraphicsApi.Unknown, ApiReason.None);
    }
}
