namespace Autopilot.Core.Routes;

/// <summary>A way of putting DLSS 5 neural rendering into a game. <see cref="RouteIds"/> maps each to the id stored in records and settings.</summary>
public enum RouteId
{
    /// <summary><c>native</c>: Krish's <c>renodx-dlss5</c> add-on hooks the DLSS calls a 64-bit D3D12 game already makes.</summary>
    Native,

    /// <summary><c>upstream</c>: matiasLombo's add-on runs the network at render resolution, before the game's own DLSS upscales.</summary>
    Upstream,

    /// <summary><c>optiscaler</c>: no ReShade; OptiScaler takes over the game's upscaler and runs the model over its output.</summary>
    OptiScaler,

    /// <summary><c>bridge</c>: NIGos' <c>dlss5-bridge</c> mirrors the game's own DLSS contract onto a private D3D12 session.</summary>
    Bridge,

    /// <summary><c>feeder</c>: DLSS5-Feeder builds a DLAA contract from ReShade's depth buffer and shader motion vectors.</summary>
    Feeder,

    /// <summary><c>standalone</c>: kibblerz's add-on does the whole pipeline itself and shows the result through a window of its own.</summary>
    Standalone,

    /// <summary><c>renodx</c>: ShortFuse's <c>renodx-dlss</c> add-on hooks D3D9, D3D11 and D3D12 presentation in-process.</summary>
    Renodx,

    /// <summary><c>uplift</c>: ghostinthecamera's add-on runs the network on the finished frame; offered, never recommended.</summary>
    Uplift,

    /// <summary><c>remix</c>: the game has an RTX Remix mod, and the neural pass runs inside its runtime.</summary>
    Remix,
}

/// <summary>The one mapping between <see cref="RouteId"/> and the ids the install record and settings store.</summary>
public static class RouteIds
{
    private static readonly Dictionary<RouteId, string> _ids = new()
    {
        [RouteId.Native] = "native",
        [RouteId.Upstream] = "upstream",
        [RouteId.OptiScaler] = "optiscaler",
        [RouteId.Bridge] = "bridge",
        [RouteId.Feeder] = "feeder",
        [RouteId.Standalone] = "standalone",
        [RouteId.Renodx] = "renodx",
        [RouteId.Uplift] = "uplift",
        [RouteId.Remix] = "remix",
    };

    private static readonly Dictionary<string, RouteId> _routes = _ids.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    // The names a person reads; product names, so they are not translated.
    private static readonly Dictionary<RouteId, string> _names = new()
    {
        [RouteId.Native] = "native",
        [RouteId.Upstream] = "neural-upstream",
        [RouteId.OptiScaler] = "optiscaler",
        [RouteId.Bridge] = "bridge",
        [RouteId.Feeder] = "feeder",
        [RouteId.Standalone] = "standalone-dlssnr",
        [RouteId.Renodx] = "renodx-dlss",
        [RouteId.Uplift] = "gitc-uplift",
        [RouteId.Remix] = "remix",
    };

    /// <summary>The stable id of <paramref name="route"/> (<c>native</c>, <c>optiscaler</c> ...), as records and settings store it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="route"/> is not one of the nine routes.</exception>
    public static string Id(this RouteId route) =>
        _ids.TryGetValue(route, out string? id) ? id : throw new ArgumentOutOfRangeException(nameof(route), route, "not a route");

    /// <summary>The route whose stable id is <paramref name="id"/>, compared exactly.</summary>
    /// <param name="id">The id as stored.</param>
    /// <param name="route">The route, when the id names one.</param>
    /// <returns>Whether <paramref name="id"/> is one of the nine ids.</returns>
    public static bool TryParse(string? id, out RouteId route)
    {
        route = default;
        return id is not null && _routes.TryGetValue(id, out route);
    }

    /// <summary>The route's name as the person reads it ("neural-upstream", "standalone-dlssnr").</summary>
    internal static string Name(this RouteId route) => _names[route];
}
