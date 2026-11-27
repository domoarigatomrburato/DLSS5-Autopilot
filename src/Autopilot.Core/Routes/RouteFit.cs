using Autopilot.Core.Executables;
using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>Whether an offered route is usable on this card for this game, and the short note the dropdown shows beside it.</summary>
internal readonly record struct Fit(bool Usable, IReadOnlyList<RouteMessage> Notes);

/// <summary>The fit table, and each route's reliability level per API and architecture.</summary>
internal static class RouteFit
{
    // The notes that never change with the game or the card.
    private static readonly Dictionary<RouteId, string> _plain = new()
    {
        [RouteId.Remix] = "RouteFitRemix",
        [RouteId.Native] = "RouteFitNative",
        [RouteId.Standalone] = "RouteFitStandalone",
        [RouteId.Uplift] = "RouteFitUplift",
        [RouteId.Renodx] = "RouteFitRenodx",
    };

    /// <summary>The route's fit for a game on a card whose architecture is <paramref name="sm"/> (null when not known).</summary>
    public static Fit Of(RouteId route, GraphicsApi api, GameRuntimes runtimes, int? sm) => route switch
    {
        RouteId.OptiScaler => OptiScaler(api, runtimes, sm),
        RouteId.Upstream => runtimes.OwnDlss ? Usable("RouteFitUpstream") : new Fit(false, [new RouteMessage("RouteFitUpstreamNeedsDlss")]),
        RouteId.Bridge => Usable(api is GraphicsApi.DX11 or GraphicsApi.Vulkan ? "RouteFitBridgeTested" : "RouteFitBridge"),
        RouteId.Feeder => Usable(runtimes.OwnDlss ? "RouteFitFeederOwnDlss" : "RouteFitFeeder"),
        _ => Usable(_plain[route]),
    };

    /// <summary>How likely the route is to work for this API and architecture; when two levels apply, the lower one.</summary>
    public static RouteReliability Level(RouteId route, GraphicsApi api, Bitness bitness) => route switch
    {
        RouteId.Native => RouteReliability.Stable,
        RouteId.Upstream or RouteId.OptiScaler or RouteId.Bridge or RouteId.Remix => RouteReliability.Beta,
        RouteId.Renodx => api == GraphicsApi.DX9 && bitness == Bitness.Bits64 ? RouteReliability.Beta : RouteReliability.Experimental,
        RouteId.Feeder => FeederLevel(api, bitness),
        _ => RouteReliability.Experimental,
    };

    private static RouteReliability FeederLevel(GraphicsApi api, Bitness bitness)
    {
        // D3D8 and D3D9 go through DXVK, and 32-bit games through the helper process.
        if (api is GraphicsApi.DX8 or GraphicsApi.DX9 || bitness == Bitness.Bits32)
        {
            return RouteReliability.Experimental;
        }

        return api is GraphicsApi.DX10 or GraphicsApi.Vulkan or GraphicsApi.OpenGL ? RouteReliability.Beta : RouteReliability.Stable;
    }

    private static Fit OptiScaler(GraphicsApi api, GameRuntimes runtimes, int? sm)
    {
        if (!runtimes.OwnDlss && runtimes.Upscaler == Upscaler.None)
        {
            return new Fit(false, [new RouteMessage("RouteFitOptiScalerNeedsUpscaler")]);
        }

        // On Vulkan nothing else in the note matters: the model has had nothing to attach to.
        List<RouteMessage> notes = api == GraphicsApi.Vulkan ? [new RouteMessage("RouteFitOptiScalerVulkan")] : OptiScalerNotes(api, runtimes);
        if (sm < CardNames.Rtx50)
        {
            notes.Add(new RouteMessage("RouteFitOptiScalerRtx50Only"));
        }

        return new Fit(true, notes);
    }

    private static List<RouteMessage> OptiScalerNotes(GraphicsApi api, GameRuntimes runtimes)
    {
        List<RouteMessage> notes = [];
        if (api == GraphicsApi.DX11)
        {
            notes.Add(new RouteMessage("RouteFitOptiScalerDx11"));
        }

        if (!runtimes.OwnDlss)
        {
            notes.Add(new RouteMessage("RouteFitOptiScalerUpscalerOn", runtimes.Upscaler == Upscaler.Xess ? "XeSS" : "FSR"));
        }

        // On D3D11 with DLSS of its own the dial is not the point: the upscaler itself changes.
        if (api != GraphicsApi.DX11 || !runtimes.OwnDlss)
        {
            notes.Add(new RouteMessage("RouteFitOptiScaler"));
        }

        return notes;
    }

    private static Fit Usable(string key) => new(true, [new RouteMessage(key)]);
}
