using Autopilot.Core.Graphics;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Tests.Library.Scanning;

namespace Autopilot.Core.Tests.Library.Emulators;

/// <summary>An emulator that cannot present through Direct3D is read as what it really draws with, whatever its imports say.</summary>
[TestClass]
public sealed class EmulatorApiTests(TestContext testContext) : IDisposable
{
    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task Eden_whose_imports_read_as_DirectX_12_is_read_as_Vulkan_and_says_why()
    {
        GameEntry entry = await ReadAsync("eden.exe", "d3d12.dll");

        Assert.AreEqual(GraphicsApi.Vulkan, entry.Api);
        Assert.AreEqual("yuzu / suyu / Eden / Citron draws with Vulkan or OpenGL only (its imports read as DX12)", entry.ApiExplanation);
        Assert.AreEqual(GraphicsApi.DX12, entry.Detected?.Api);
        Assert.IsFalse(entry.ApiSetByHand);
    }

    [TestMethod]
    [DataRow("Cemu.exe", GraphicsApi.Vulkan)]
    [DataRow("rpcs3.exe", GraphicsApi.Vulkan)]
    [DataRow("Ryujinx.exe", GraphicsApi.Vulkan)]
    [DataRow("shadPS4.exe", GraphicsApi.Vulkan)]
    [DataRow("azahar.exe", GraphicsApi.Vulkan)]
    [DataRow("xemu.exe", GraphicsApi.Vulkan)]
    [DataRow("Vita3K.exe", GraphicsApi.Vulkan)]
    [DataRow("Play.exe", GraphicsApi.Vulkan)]
    [DataRow("melonDS.exe", GraphicsApi.OpenGL)]
    [DataRow("mGBA.exe", GraphicsApi.OpenGL)]
    public async Task An_emulator_with_no_Direct3D_backend_is_read_as_Vulkan_or_for_the_OpenGL_only_ones_OpenGL(string executable, GraphicsApi api)
    {
        GameEntry entry = await ReadAsync(executable, "d3d11.dll");

        Assert.AreEqual(api, entry.Api);
        StringAssert.EndsWith(entry.ApiExplanation, "draws with Vulkan or OpenGL only (its imports read as DX11)", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("d3d9.dll", "DX9")]
    [DataRow("dxgi.dll", "DX11 or DX12")]
    public async Task Any_Direct3D_the_imports_name_is_read_past(string import, string named)
    {
        GameEntry entry = await ReadAsync("rpcs3.exe", import);

        Assert.AreEqual(GraphicsApi.Vulkan, entry.Api);
        Assert.AreEqual($"RPCS3 draws with Vulkan or OpenGL only (its imports read as {named})", entry.ApiExplanation);
    }

    [TestMethod]
    [DataRow("duckstation-qt-x64.exe")]
    [DataRow("retroarch.exe")]
    [DataRow("xenia_canary.exe")]
    public async Task An_emulator_that_can_present_through_Direct3D_keeps_what_its_imports_say(string executable)
    {
        GameEntry entry = await ReadAsync(executable, "d3d12.dll");

        Assert.AreEqual(GraphicsApi.DX12, entry.Api);
        Assert.AreEqual("imports d3d12.dll statically", entry.ApiExplanation);
    }

    [TestMethod]
    public async Task An_emulator_whose_imports_already_say_OpenGL_keeps_the_executables_reason()
    {
        GameEntry entry = await ReadAsync("eden.exe", "opengl32.dll");

        Assert.AreEqual(entry.Detected?.Api, entry.Api);
        Assert.AreEqual(entry.Detected?.Explanation, entry.ApiExplanation);
    }

    [TestMethod]
    public async Task An_api_chosen_by_hand_is_never_overridden()
    {
        _bed.Choices.Set(_bed.PathOf("Emu"), new GameChoices(GraphicsApi.DX11, null, null));

        GameEntry entry = await ReadAsync("eden.exe", "d3d12.dll");

        Assert.AreEqual((GraphicsApi.DX11, "set by hand (detected DX12)", true), (entry.Api, entry.ApiExplanation, entry.ApiSetByHand));
    }

    [TestMethod]
    public async Task A_store_game_called_Play_keeps_the_Direct3D_its_imports_say()
    {
        _ = _bed.Exe("Emu/Play.exe", imports: "d3d11.dll");

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Play", "Emu", GameSource.Steam), Ct);

        Assert.AreEqual(GraphicsApi.DX11, entry.Api);
    }

    private async Task<GameEntry> ReadAsync(string executable, string import)
    {
        _ = _bed.Exe($"Emu/{executable}", imports: import);
        return await _bed.ReadAsync(_bed.Game("Emu", "Emu", GameSource.Emulator, $"Emu/{executable}"), Ct);
    }
}
