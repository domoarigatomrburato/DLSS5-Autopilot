using Autopilot.Core.Graphics;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>A 32-bit Unreal Engine 3 game names d3d11.dll for an option and draws with Direct3D 9 unless its config says 11.</summary>
[TestClass]
public sealed class Unreal3Tests(TestContext testContext) : GraphicsTestBase(testContext)
{
    private const string Allow = "[SystemSettings]\nAllowD3D11=True\n";
    private const string Deny = "[SystemSettings]\nAllowD3D11 = False ; the player's choice\n";

    [TestMethod]
    public async Task Life_is_strange_naming_d3d11_is_directx_9()
    {
        ApiDecision decision = await DecideAsync(LifeIsStrange());

        Is(decision, GraphicsApi.DX9, ApiReason.Unreal3, "Unreal Engine 3", "CookedPCConsole", "d3d11.dll its exe names");
        Assert.AreEqual(ApiConfidence.Guessed, decision.Confidence);
    }

    [TestMethod]
    public async Task A_shipped_engine_ini_allowing_d3d11_makes_it_dx11()
    {
        string exe = LifeIsStrange();
        TestFiles.Plant(In("LifeIsStrange", "LifeIsStrangeGame", "Config", "DefaultEngine.ini"), Allow);

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.Unreal3, "DefaultEngine.ini", "AllowD3D11=True");
    }

    [TestMethod]
    public async Task The_players_copy_decides_before_the_shipped_one()
    {
        string exe = LifeIsStrange();
        TestFiles.Plant(In("LifeIsStrange", "LifeIsStrangeGame", "Config", "DefaultEngine.ini"), Allow);
        TestFiles.Plant(PlayerIni("Documents", "Square Enix LifeIsStrange"), Deny);

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.Unreal3, "LifeIsStrangeEngine.ini", "AllowD3D11=False");
    }

    [TestMethod]
    public async Task The_players_copy_under_onedrive_counts_too()
    {
        string exe = LifeIsStrange();
        TestFiles.Plant(PlayerIni(Path.Join("OneDrive", "Documents"), "Life is Strange"), Allow);

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.Unreal3, Path.Join("OneDrive", "Documents"));
    }

    [TestMethod]
    public async Task A_players_copy_under_another_games_name_does_not_count()
    {
        string exe = LifeIsStrange();
        TestFiles.Plant(PlayerIni("Documents", "Batman Arkham Origins"), Allow);

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.Unreal3, "CookedPC");
    }

    [TestMethod]
    public async Task A_64_bit_exe_in_the_same_layout_is_not_touched_by_the_rule() =>
        Is(await DecideAsync(LifeIsStrange(Bits64)), GraphicsApi.DX11, ApiReason.RunTimeName);

    [TestMethod]
    public async Task Without_a_cooked_folder_the_layout_is_not_unreal_3()
    {
        string exe = LifeIsStrange();
        Directory.Delete(In("LifeIsStrange", "LifeIsStrangeGame", "CookedPCConsole"));

        Is(await DecideAsync(exe), GraphicsApi.DX11, ApiReason.RunTimeName);
    }

    private string LifeIsStrange(ushort machine = Bits32)
    {
        Folder(Work, "LifeIsStrange", "LifeIsStrangeGame", "CookedPCConsole");
        return Naming(Path.Join("LifeIsStrange", "Binaries", "Win32", "LifeIsStrange.exe"), machine, [], "d3d11.dll", "d3d9.dll");
    }

    private string PlayerIni(string documents, string owner) =>
        Path.Join(In(documents), "My Games", owner, "LifeIsStrangeGame", "Config", "LifeIsStrangeEngine.ini");

    [TestMethod]
    public async Task An_engine_ini_that_does_not_say_leaves_it_directx_9()
    {
        string exe = LifeIsStrange();
        TestFiles.Plant(In("LifeIsStrange", "LifeIsStrangeGame", "Config", "DefaultEngine.ini"), "[SystemSettings]\nAllowD3D11=Maybe\n");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.Unreal3, "CookedPC");
    }

    [TestMethod]
    public async Task An_engine_ini_over_4_mib_is_not_read()
    {
        string exe = LifeIsStrange();
        TestFiles.Plant(In("LifeIsStrange", "LifeIsStrangeGame", "Config", "DefaultEngine.ini"), Allow + new string(';', 4 * 1024 * 1024));

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.Unreal3, "CookedPC");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task An_engine_ini_whose_content_is_refused_is_not_read()
    {
        string exe = LifeIsStrange();
        string ini = In("LifeIsStrange", "LifeIsStrangeGame", "Config", "DefaultEngine.ini");
        TestFiles.Plant(ini, Allow);
        Refuse(ini);

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.Unreal3, "CookedPC");
    }
}
