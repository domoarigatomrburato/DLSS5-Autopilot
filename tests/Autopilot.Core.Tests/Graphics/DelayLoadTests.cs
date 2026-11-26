using Autopilot.Core.Graphics;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>With no static graphics import, a graphics DLL in the delay-load table is a dependency the linker recorded.</summary>
[TestClass]
public sealed class DelayLoadTests(TestContext testContext) : GraphicsTestBase(testContext)
{
    [TestMethod]
    [DataRow("d3d12.dll", GraphicsApi.DX12)]
    [DataRow("d3d11.dll", GraphicsApi.DX11)]
    [DataRow("d3d10_1.dll", GraphicsApi.DX10)]
    [DataRow("dxgi.dll", GraphicsApi.Dxgi)]
    [DataRow("vulkan-1.dll", GraphicsApi.Vulkan)]
    [DataRow("opengl32.dll", GraphicsApi.OpenGL)]
    [DataRow("d3d9.dll", GraphicsApi.DX9)]
    [DataRow("d3d8.dll", GraphicsApi.DX8)]
    public async Task A_32_bit_game_that_only_delay_loads_a_graphics_dll_is_read_from_that_table(string dll, GraphicsApi api)
    {
        ApiDecision decision = await DecideAsync(Write(new TestPe(Bits32).WithImports("kernel32.dll").WithDelayImports("xinput1_3.dll", dll)));

        Is(decision, api, ApiReason.DelayLoad, "delay-load table", dll);
        Assert.AreEqual(ApiConfidence.Read, decision.Confidence);
    }

    [TestMethod]
    [DataRow("d3d11.dll", "d3d12.dll", GraphicsApi.DX12)]
    [DataRow("d3d10.dll", "d3d11.dll", GraphicsApi.DX11)]
    [DataRow("dxgi.dll", "d3d10core.dll", GraphicsApi.DX10)]
    [DataRow("vulkan-1.dll", "dxgi.dll", GraphicsApi.Dxgi)]
    [DataRow("opengl32.dll", "vulkan-1.dll", GraphicsApi.Vulkan)]
    [DataRow("d3d9.dll", "opengl32.dll", GraphicsApi.OpenGL)]
    [DataRow("d3d8.dll", "d3d9.dll", GraphicsApi.DX9)]
    public async Task The_higher_ranked_delay_load_decides(string lower, string higher, GraphicsApi api) =>
        Is(await DecideAsync(Write(new TestPe(Bits32).WithDelayImports(lower, higher))), api, ApiReason.DelayLoad, higher);

    [TestMethod]
    public async Task A_64_bit_game_that_only_delay_loads_d3d8_is_unknown() =>
        Is(await DecideAsync(Write(new TestPe().WithDelayImports("d3d8.dll"))), GraphicsApi.Unknown, ApiReason.None, "d3d8.dll", "32-bit only");

    [TestMethod]
    public async Task A_static_graphics_import_decides_before_the_delay_load_table() =>
        Is(await DecideAsync(Write(new TestPe(Bits32).WithImports("d3d8.dll").WithDelayImports("d3d12.dll"))), GraphicsApi.DX8, ApiReason.StaticImport);

    [TestMethod]
    [DataRow("d3d11.dll")]
    [DataRow("d3d9.dll")]
    public async Task A_game_that_delay_loads_a_renderer_and_ships_an_agility_sdk_is_dx12(string dll)
    {
        string exe = Write(new TestPe().WithDelayImports(dll));
        Dll(exe, Path.Join("D3D12", "D3D12Core.dll"));

        Is(await DecideAsync(exe), GraphicsApi.DX12, ApiReason.AgilitySdk, "D3D12Core.dll");
    }

    [TestMethod]
    public async Task The_delay_load_table_decides_before_the_run_time_search()
    {
        string exe = Write(new TestPe().WithDelayImports("d3d9.dll"));
        Dll(exe, "renderer.dll", Bits64, "d3d12.dll");

        Is(await DecideAsync(exe), GraphicsApi.DX9, ApiReason.DelayLoad, "d3d9.dll");
    }
}
