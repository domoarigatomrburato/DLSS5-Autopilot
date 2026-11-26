using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>The rules for a graphics DLL in the executable's own import table, in their order.</summary>
[TestClass]
public sealed class StaticImportTests(TestContext testContext) : GraphicsTestBase(testContext)
{
    [TestMethod]
    [DataRow("d3d12.dll", GraphicsApi.DX12)]
    [DataRow("d3d11.dll", GraphicsApi.DX11)]
    [DataRow("d3d10.dll", GraphicsApi.DX10)]
    [DataRow("d3d10_1.dll", GraphicsApi.DX10)]
    [DataRow("d3d10core.dll", GraphicsApi.DX10)]
    public async Task A_direct3d_import_is_read_as_that_direct3d(string dll, GraphicsApi api)
    {
        ApiDecision decision = await DecideAsync(Game(imports: ["kernel32.dll", dll]));

        Is(decision, api, ApiReason.StaticImport, $"imports {dll} statically");
        Assert.AreEqual(ApiConfidence.Read, decision.Confidence);
    }

    [TestMethod]
    [DataRow("vulkan-1.dll", GraphicsApi.Vulkan)]
    [DataRow("opengl32.dll", GraphicsApi.OpenGL)]
    [DataRow("d3d9.dll", GraphicsApi.DX9)]
    [DataRow("d3d8.dll", GraphicsApi.DX8)]
    public async Task A_32_bit_game_with_one_non_dxgi_import_draws_with_it(string dll, GraphicsApi api) =>
        Is(await DecideAsync(Game(machine: Bits32, imports: dll)), api, ApiReason.StaticImport, $"imports {dll}, no DXGI");

    [TestMethod]
    public async Task A_game_that_imports_only_dxgi_is_direct3d_11_or_12_not_settled() =>
        Is(await DecideAsync(Game(imports: "dxgi.dll")), GraphicsApi.Dxgi, ApiReason.StaticImport, "dxgi.dll", "11 or 12");

    [TestMethod]
    [DataRow("d3d12.dll", "d3d11.dll", GraphicsApi.DX12)]
    [DataRow("d3d11.dll", "d3d10.dll", GraphicsApi.DX11)]
    [DataRow("d3d10.dll", "dxgi.dll", GraphicsApi.DX10)]
    [DataRow("dxgi.dll", "vulkan-1.dll", GraphicsApi.Dxgi)]
    [DataRow("vulkan-1.dll", "opengl32.dll", GraphicsApi.Vulkan)]
    [DataRow("opengl32.dll", "d3d9.dll", GraphicsApi.OpenGL)]
    [DataRow("d3d9.dll", "d3d8.dll", GraphicsApi.DX9)]
    public async Task The_higher_ranked_import_decides(string higher, string lower, GraphicsApi api) =>
        Is(await DecideAsync(Game(machine: Bits32, imports: [lower, higher])), api, ApiReason.StaticImport, higher);

    [TestMethod]
    public async Task Hell_is_us_importing_dxgi_and_opengl32_is_not_an_opengl_game() =>
        Is(await DecideAsync(Game("HellIsUs.exe", imports: ["opengl32.dll", "dxgi.dll"])), GraphicsApi.Dxgi, ApiReason.StaticImport);

    [TestMethod]
    public async Task An_import_that_merely_contains_a_graphics_name_is_not_one()
    {
        string exe = Game(machine: Bits32, imports: ["xd3d11.dll", "libd3d12.dll", "d3d9.dll"]);

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.StaticImport);
    }

    [TestMethod]
    public async Task A_64_bit_game_that_imports_d3d8_is_unknown_because_directx_8_is_32_bit_only()
    {
        ApiDecision decision = await DecideAsync(Game(imports: "d3d8.dll"));

        Is(decision, GraphicsApi.Unknown, ApiReason.None, "imports d3d8.dll", "32-bit only", "64-bit");
        Assert.AreEqual(ApiConfidence.None, decision.Confidence);
    }

    [TestMethod]
    public async Task An_opengl_game_with_no_engine_layout_is_opengl_even_with_dlls_beside_it()
    {
        string exe = Game(imports: "opengl32.dll");
        Dll(exe, "engine.dll", Bits64, "d3d11.dll");

        Is(await DecideAsync(exe), GraphicsApi.OpenGL, ApiReason.StaticImport, "opengl32.dll");
    }

    [TestMethod]
    public async Task A_cancelled_decision_stops_with_cancellation()
    {
        string exe = Game(imports: "d3d12.dll");
        ExecutableReading reading = await Executable.ReadAsync(exe, Ct);

        await Assert.ThrowsAsync<OperationCanceledException>(() => GraphicsApiDecision.DecideAsync(exe, reading, Search, new CancellationToken(canceled: true)));
    }
}
