using Autopilot.Core.Components.Sources;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install.Plans;

/// <summary>The file names the route plans know by heart: ReShade's proxy names, each route's add-ons, and what a hand-installed OptiScaler brings.</summary>
internal static class PlanNames
{
    /// <summary>ReShade's settings file, beside the executable.</summary>
    public const string ReShadeIni = "ReShade.ini";

    /// <summary>Every name ReShade loads as beside an executable: the five of "ReShade loads as", and Direct3D 9's.</summary>
    public static IReadOnlyList<string> ReShadeProxies { get; } = [.. Enum.GetValues<ReShadeProxy>().Select(proxy => proxy.FileName()), KnownFiles.D3d9];

    /// <summary>What a hand-installed OptiScaler brings beside its DLL: its settings and its forwarder.</summary>
    public static IReadOnlyList<string> OptiScalerCompanions { get; } = [KnownFiles.OptiScalerIni, KnownFiles.OptiScalerForwarder];

    // routes.md, "Route-switch purging": each route's own add-ons.
    private static readonly (RouteId Route, string[] Files)[] _addOns =
    [
        (RouteId.Feeder, [FeederRelease.Addon64, FeederRelease.Addon32, FeederRelease.HelperAddon]),
        (RouteId.Bridge, ["dlss5-bridge.addon64"]),
        (RouteId.Renodx, ["renodx-dlss.addon64"]),
        (RouteId.Upstream, [ReleaseComponents.UpstreamAsset]),
        (RouteId.Standalone, [StandaloneComponent.StandaloneAddon, StandaloneComponent.StandaloneForwarder]),
        (RouteId.Uplift, [ListedComponents.UpliftAddon64, ListedComponents.UpliftAddon32, ListedComponents.UpliftHelper]),
    ];

    // The routes that take renodx-dlss5 out as well: those that do not load it.
    private static readonly RouteId[] _withoutRenodx = [RouteId.Renodx, RouteId.Upstream, RouteId.Standalone, RouteId.Uplift, RouteId.Remix];

    /// <summary>Every name the purge can take out, on any route: what the survey looks at.</summary>
    public static IReadOnlyList<string> AllAddOns { get; } = [.. _addOns.SelectMany(row => row.Files), KnownFiles.RenodxAddon, KnownFiles.OldBridgeAddon];

    /// <summary><paramref name="route"/>'s own add-ons, as the purge table lists them: what its plan places and every other route's takes out.</summary>
    public static IReadOnlyList<string> AddOnsOf(RouteId route) => Array.Find(_addOns, row => row.Route == route).Files ?? [];

    /// <summary>The add-ons an install of <paramref name="route"/> takes out of ReShade's reach: every other route's, <c>renodx-dlss5</c> where the route does not load it, and the older bridge add-on.</summary>
    public static IReadOnlyList<string> PurgedOn(RouteId route) =>
        [
            .. _addOns.Where(row => row.Route != route).SelectMany(row => row.Files),
            .. _withoutRenodx.Contains(route) ? [KnownFiles.RenodxAddon] : Array.Empty<string>(),
            KnownFiles.OldBridgeAddon,
        ];
}
