using Autopilot.Core.Graphics;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>Unity and Unreal layouts settle the renderer by themselves, whatever the bytes name.</summary>
[TestClass]
public sealed class EngineTests(TestContext testContext) : GraphicsTestBase(testContext)
{
    private static readonly string _shipping = Path.Join("Game", "Binaries", "Win64", "Game-Win64-Shipping.exe");

    [TestMethod]
    [DataRow("NivalisNights.exe")]
    [DataRow("DreadDelusion.exe")]
    public async Task A_unity_title_naming_opengl32_is_dx11_and_the_reason_names_unity(string name)
    {
        string exe = Naming(name, Bits64, [], "opengl32.dll", "d3d12.dll", "vulkan-1.dll");
        Junk(exe, "UnityPlayer.dll");

        ApiDecision decision = await DecideAsync(exe);

        Is(decision, GraphicsApi.DX11, ApiReason.Unity, "Unity", "-force-d3d12");
        Assert.AreEqual(ApiConfidence.Guessed, decision.Confidence);
    }

    [TestMethod]
    public async Task A_unity_game_importing_opengl32_is_dx11()
    {
        string exe = Game(imports: "opengl32.dll");
        Junk(exe, "UnityPlayer.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.Unity);
    }

    [TestMethod]
    public async Task The_unity_rule_is_not_cut_by_65_other_dlls()
    {
        string exe = Naming("Game.exe", Bits32, [], "opengl32.dll");
        for (int i = 0; i < 65; i++)
        {
            Junk(exe, $"a{i:00}.dll");
        }

        Junk(exe, "UnityPlayer.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.Unity);
    }

    [TestMethod]
    [DataRow("Win64")]
    [DataRow("wingdk")]
    [DataRow("WinARM64")]
    public async Task An_unreal_shipping_exe_naming_opengl32_is_direct3d_11_or_12(string platform)
    {
        Folder(Work, "Engine");
        string exe = Naming(Path.Join("WARDOGS", "Binaries", platform, "WARDOGS-Win64-Shipping.exe"), Bits64, [], "opengl32.dll");

        Is(await DecideAsync(exe), GraphicsApi.Dxgi, ApiReason.Unreal, "Unreal Engine", "Direct3D 12 or 11");
    }

    [TestMethod]
    public async Task An_unreal_exe_importing_opengl32_is_direct3d_11_or_12()
    {
        Folder(Work, "Engine");

        Is(await DecideAsync(Game(_shipping, imports: "opengl32.dll")), GraphicsApi.Dxgi, ApiReason.Unreal);
    }

    [TestMethod]
    public async Task Without_the_engine_folder_the_layout_is_not_unreal() =>
        Is(await DecideAsync(Naming(_shipping, Bits64, [], "opengl32.dll")), GraphicsApi.OpenGL, ApiReason.RunTimeName);

    [TestMethod]
    public async Task The_engine_rule_decides_before_the_exes_own_strings()
    {
        Folder(Work, "Engine");

        Is(await DecideAsync(Naming(_shipping, Bits64, [], "d3d9.dll")), GraphicsApi.Dxgi, ApiReason.Unreal);
    }
}
