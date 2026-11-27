using Autopilot.Core.Executables;
using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>Games and cards as the routes, the settings page and the install read them.</summary>
internal static class RouteFactsBeds
{
    /// <summary>A 64-bit game read whole, one executable, shipping nothing but what the arguments say, the card not read.</summary>
    public static RouteFacts Game(GraphicsApi api = GraphicsApi.DX12, bool ownDlss = false, bool rayReconstruction = false, string executable = "game.exe") =>
        new(api, new(ownDlss, [], Upscaler.None, [], rayReconstruction, false, null), false, executable)
        {
            Bitness = Bitness.Bits64,
            ArchitectureRead = true,
            Executables = 1,
            DlssBesideExecutable = false,
            LoaderNameFree = false,
            Gpu = null,
        };

    /// <summary>An RTX 40 card, the game's own DLSS and frame generation, and a loader name free in its imports: the unlock is offered on a ReShade route.</summary>
    public static RouteFacts Rtx40Game(GraphicsApi api) =>
        Game(api, ownDlss: true) with { Runtimes = Game(api, ownDlss: true).Runtimes with { ShipsFrameGeneration = true }, LoaderNameFree = true, Gpu = Card(0x10DE, "NVIDIA GeForce RTX 4070") };

    /// <summary>A card as Windows names it.</summary>
    public static GpuFacts Card(int vendor, string name) => GpuFacts.From(new GpuReading([new(vendor, name, false)], null, []));

    /// <summary>An NVIDIA card beside the processor's own graphics.</summary>
    public static GpuFacts TwoCards { get; } =
        GpuFacts.From(new GpuReading([new(0x10DE, "NVIDIA GeForce RTX 4070", false), new(0x8086, "Intel(R) UHD Graphics 770", false)], null, []));
}
