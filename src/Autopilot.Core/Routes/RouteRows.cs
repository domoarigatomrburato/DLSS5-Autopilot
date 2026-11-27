using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>The rules table: the first row that matches the game gives its options in dropdown order and the recommendation.</summary>
internal static class RouteRows
{
    // Rows 7, 8, 8a, 9 and 10.
    private static readonly RouteId[] _d3d11OwnDlss = [RouteId.Bridge, RouteId.OptiScaler, RouteId.Feeder, RouteId.Standalone, RouteId.Renodx];
    private static readonly RouteId[] _d3d12OwnDlss = [RouteId.Native, RouteId.Upstream, RouteId.OptiScaler, RouteId.Bridge, RouteId.Feeder, RouteId.Standalone, RouteId.Renodx];
    private static readonly RouteId[] _eitherOwnDlss = [RouteId.OptiScaler, RouteId.Bridge, RouteId.Feeder, RouteId.Standalone, RouteId.Renodx];
    private static readonly RouteId[] _upscaler = [RouteId.Feeder, RouteId.OptiScaler, RouteId.Bridge, RouteId.Standalone, RouteId.Renodx];
    private static readonly RouteId[] _nothing = [RouteId.Feeder, RouteId.Bridge, RouteId.Standalone, RouteId.Renodx];

    /// <summary>The row for a game whose architecture is known and that is not D3D8 on 64-bit.</summary>
    public static RouteDraft For(GraphicsApi api, Bitness bitness, GameRuntimes runtimes)
    {
        if (api == GraphicsApi.DX10)
        {
            return new RouteDraft([RouteId.Feeder], RouteId.Feeder, new RouteMessage("RouteDx10"));
        }

        if (bitness == Bitness.Bits32)
        {
            return new RouteDraft([RouteId.Feeder], RouteId.Feeder, new RouteMessage("Route32Bit")) { Notes = Dxvk(api) };
        }

        return api switch
        {
            GraphicsApi.DX9 => new RouteDraft([RouteId.Renodx], RouteId.Renodx, new RouteMessage("RouteDx9On64")),
            GraphicsApi.Vulkan => runtimes.OwnDlss
                ? new RouteDraft([RouteId.Bridge, RouteId.Feeder], RouteId.Bridge, new RouteMessage("RouteVulkanOwnDlss"))
                : new RouteDraft([RouteId.Feeder, RouteId.Bridge], RouteId.Feeder, new RouteMessage("RouteVulkan")),
            GraphicsApi.OpenGL => new RouteDraft([RouteId.Feeder], RouteId.Feeder, new RouteMessage("RouteOpenGl")),
            _ => runtimes.OwnDlss ? OwnDlss(api) : NoDlss(api, runtimes),
        };
    }

    // Rows 7, 8 and 8a: D3D11, D3D12, or not settled between them (DXGI, unknown).
    private static RouteDraft OwnDlss(GraphicsApi api) => api switch
    {
        GraphicsApi.DX11 => new RouteDraft(_d3d11OwnDlss, RouteId.Bridge, new RouteMessage("RouteDx11OwnDlss")),
        GraphicsApi.DX12 => new RouteDraft(_d3d12OwnDlss, RouteId.Native, new RouteMessage("RouteDx12OwnDlss")),
        _ => AskApi(_eitherOwnDlss),
    };

    // Rows 9 and 10; an unknown API gets their options and the API question instead of a recommendation.
    private static RouteDraft NoDlss(GraphicsApi api, GameRuntimes runtimes)
    {
        bool upscaler = runtimes.Upscaler != Upscaler.None;
        if (api == GraphicsApi.Unknown)
        {
            return AskApi(upscaler ? _upscaler : _nothing);
        }

        return upscaler
            ? new RouteDraft(_upscaler, RouteId.Feeder, new RouteMessage("RouteUpscaler", UpscalerName(runtimes.Upscaler), string.Join(", ", runtimes.UpscalerEvidence.Take(3))))
            : new RouteDraft(_nothing, RouteId.Feeder, new RouteMessage("RouteNoDlss"));
    }

    private static RouteDraft AskApi(RouteId[] options) =>
        new(options, null, new RouteMessage("RouteAskApi")) { Asks = RouteQuestion.Api };

    /// <summary>"FSR 2/3" or "XeSS", as the texts name a shipped upscaler.</summary>
    public static string UpscalerName(Upscaler upscaler) => upscaler == Upscaler.Xess ? "XeSS" : "FSR 2/3";

    private static RouteMessage[] Dxvk(GraphicsApi api) => api switch
    {
        GraphicsApi.DX8 => [new RouteMessage("RouteNoteDxvkDx8")],
        GraphicsApi.DX9 => [new RouteMessage("RouteNoteDxvkDx9")],
        _ => [],
    };
}
