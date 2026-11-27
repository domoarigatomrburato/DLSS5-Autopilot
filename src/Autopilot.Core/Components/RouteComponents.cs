using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Components;

/// <summary>Where the feeder reads its motion vectors from: the number written as <c>DLSS5_MV_PROVIDER</c>.</summary>
public enum MotionProvider
{
    /// <summary>A generic <c>texMotionVectors</c> shader the person installs.</summary>
    Generic = 0,

    /// <summary>iMMERSE Launchpad, which the person installs.</summary>
    Launchpad = 1,

    /// <summary>VORT's <c>vort_Motion.fx</c>.</summary>
    Vort = 2,

    /// <summary>LumeniteFX's <c>lumenite_Kernel.fx</c>, the default.</summary>
    LumeniteKernel = 3,

    /// <summary>LumeniteFX's <c>lumenite_QuantMotion.fx</c>.</summary>
    LumeniteQuantMotion = 4,

    /// <summary>The feeder's own provider (feeder 2.0.0 or newer).</summary>
    BuiltIn = 5,
}

/// <summary>What the components a route needs depend on, as the game was read.</summary>
/// <param name="Api">The graphics API in use: the person's choice, else what the executable said.</param>
/// <param name="Runtimes">What the game ships of its own.</param>
/// <param name="IsReEngine">A Capcom RE Engine game (its marker file is in the folder), which needs REFramework first.</param>
/// <param name="ExecutableName">The executable's file name, for the games known to need DXVK.</param>
public sealed record RouteFacts(GraphicsApi Api, GameRuntimes Runtimes, bool IsReEngine, string ExecutableName);

/// <summary>Which catalog components a route installs, from the route, the game, and the options only the install knows.</summary>
public static class RouteComponents
{
    // They close themselves the moment a ReShade DLL hooks Direct3D 11.
    private static readonly string[] _needDxvk = ["mgsvtpp.exe", "mgsvmgo.exe", "mgsgzs.exe"];

    /// <summary>
    /// The components <paramref name="route"/> installs, in catalog order: what every ReShade
    /// route installs, the route's own parts, DXVK when the game goes through it, REFramework on an RE
    /// Engine game.
    /// </summary>
    /// <param name="route">The chosen route.</param>
    /// <param name="facts">The game as read.</param>
    /// <param name="dxvk">The DXVK box on a Direct3D 11 game; <see langword="null"/> for its default, on for a game known to need it.</param>
    /// <param name="provider">The feeder's motion-vector provider; <see langword="null"/> for the default (see <see cref="ProviderFor"/>).</param>
    /// <param name="multiFrameGeneration">The RTX 40 multi-frame-generation box (ReShade routes, Direct3D 12 and Vulkan).</param>
    /// <param name="rayReconstruction">A ray-reconstruction build chosen, swapped over the game's own <c>nvngx_dlssd.dll</c>.</param>
    /// <param name="swapRemixRuntime">"Swap the Remix runtime" on the remix route.</param>
    public static IReadOnlyList<string> For(
        RouteId route,
        RouteFacts facts,
        bool? dxvk = null,
        MotionProvider? provider = null,
        bool multiFrameGeneration = false,
        bool rayReconstruction = false,
        bool swapRemixRuntime = false)
    {
        ArgumentNullException.ThrowIfNull(facts);
        HashSet<string> ids = [ComponentIds.Dlssnr, .. Own(route, facts, provider, swapRemixRuntime)];
        if (IsReShadeRoute(route))
        {
            ids.UnionWith(EveryReShadeRoute(route, facts, multiFrameGeneration, rayReconstruction));
        }

        if (UsesDxvk(route, facts, dxvk))
        {
            _ = ids.Add(ComponentIds.Dxvk);
        }

        if (facts.IsReEngine && route != RouteId.Remix)
        {
            _ = ids.Add(ComponentIds.Reframework);
        }

        return [.. ComponentCatalog.All.Select(entry => entry.Id).Where(ids.Contains)];
    }

    /// <summary>The provider the feeder gets: the choice, LumeniteFX's kernel by default; VORT on an OpenGL game, where LumeniteFX reads no motion.</summary>
    public static MotionProvider ProviderFor(GraphicsApi api, MotionProvider? provider)
    {
        MotionProvider chosen = provider ?? MotionProvider.LumeniteKernel;
        return api == GraphicsApi.OpenGL && chosen is MotionProvider.LumeniteKernel or MotionProvider.LumeniteQuantMotion ? MotionProvider.Vort : chosen;
    }

    /// <summary>Whether the executable, by name or Windows path, is one known to close itself on a ReShade DLL, which DXVK serves by default.</summary>
    public static bool NeedsDxvk(string executableName)
    {
        ArgumentNullException.ThrowIfNull(executableName);
        return _needDxvk.Contains(executableName[(executableName.LastIndexOfAny(['/', '\\']) + 1)..], StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsReShadeRoute(RouteId route) => route is not (RouteId.OptiScaler or RouteId.Remix);

    private static IEnumerable<string> Own(RouteId route, RouteFacts facts, MotionProvider? provider, bool swapRemixRuntime) => route switch
    {
        RouteId.Native => [ComponentIds.Renodx],
        RouteId.Upstream => [ComponentIds.Upstream],
        RouteId.Bridge => [ComponentIds.Bridge, ComponentIds.Renodx],
        RouteId.Feeder => [ComponentIds.Feeder, ComponentIds.Renodx, ComponentIds.ShaderHeaders, .. ProviderShaders(ProviderFor(facts.Api, provider))],
        RouteId.Standalone => [ComponentIds.Standalone, ComponentIds.ShaderHeaders, ComponentIds.Vort, ComponentIds.Dlssg],
        RouteId.Renodx => [ComponentIds.RenodxSf],
        RouteId.Uplift => [ComponentIds.Uplift],

        // nvngx_dlss.dll only for a game redirected from FSR or XeSS: one with DLSS keeps its own.
        RouteId.OptiScaler => facts.Runtimes.OwnDlss ? [ComponentIds.OptiScaler] : [ComponentIds.OptiScaler, ComponentIds.Dlss],
        _ => swapRemixRuntime ? [ComponentIds.RemixRuntime] : [],
    };

    // Providers 0 and 1 are shaders the person installs, and 5 is the feeder's own.
    private static IEnumerable<string> ProviderShaders(MotionProvider provider) => provider switch
    {
        MotionProvider.LumeniteKernel or MotionProvider.LumeniteQuantMotion => [ComponentIds.Lumenite],
        MotionProvider.Vort => [ComponentIds.Vort],
        _ => [],
    };

    private static IEnumerable<string> EveryReShadeRoute(RouteId route, RouteFacts facts, bool multiFrameGeneration, bool rayReconstruction)
    {
        yield return ComponentIds.ReShade;

        // upstream and uplift run the network themselves: no nvngx_dlss.dll goes in.
        if (route is not (RouteId.Upstream or RouteId.Uplift))
        {
            yield return ComponentIds.Dlss;
        }

        if (rayReconstruction && facts.Runtimes.ShipsRayReconstruction)
        {
            yield return ComponentIds.Dlssd;
        }

        if (multiFrameGeneration && facts.Api is GraphicsApi.DX12 or GraphicsApi.Vulkan)
        {
            yield return ComponentIds.Mfg;
            yield return ComponentIds.AsiLoader;
        }
    }

    // DXVK on native, bridge and the feeder only: forced for Direct3D 9 and 8, by the box for 11. A
    // game known to need it keeps it by default whatever API was set by hand: the detected one decides.
    private static bool UsesDxvk(RouteId route, RouteFacts facts, bool? box) =>
        route is RouteId.Native or RouteId.Bridge or RouteId.Feeder
        && facts.Api switch
        {
            GraphicsApi.DX9 or GraphicsApi.DX8 => true,
            _ when NeedsDxvk(facts.ExecutableName) => box ?? true,
            GraphicsApi.DX11 => box ?? false,
            _ => false,
        };
}
