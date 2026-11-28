using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Executables;
using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library;

namespace Autopilot.Core.Routes;

/// <summary>What the components a route needs depend on, as the game was read.</summary>
/// <param name="Api">The graphics API in use: the person's choice, else what the executable said.</param>
/// <param name="Runtimes">What the game ships of its own.</param>
/// <param name="IsReEngine">A Capcom RE Engine game (its marker file is in the folder), which needs REFramework first.</param>
/// <param name="ExecutableName">The executable's file name, for the games known to need DXVK.</param>
public sealed record RouteFacts(GraphicsApi Api, GameRuntimes Runtimes, bool IsReEngine, string ExecutableName)
{
    /// <summary>The architecture, read or set by hand; null when not known.</summary>
    public required Bitness? Bitness { get; init; }

    /// <summary>Whether the executable's architecture could be read; when not, the person sets it.</summary>
    public required bool ArchitectureRead { get; init; }

    /// <summary>How many executables the folder holds that could be the game.</summary>
    public required int Executables { get; init; }

    /// <summary>A game's own <c>nvngx_dlss.dll</c> sits beside the executable.</summary>
    public required bool DlssBesideExecutable { get; init; }

    /// <summary>The executable imports one of the multi-frame-generation loader's names that no other mod uses.</summary>
    public required bool LoaderNameFree { get; init; }

    /// <summary>The card and the driver, as the routes and the install read them; null when they could not be read.</summary>
    public required GpuFacts? Gpu { get; init; }

    /// <summary>
    /// The facts of <paramref name="game"/> as the library read it, with what the game ships and the card. The
    /// architecture counts as read when the executable said it: a protected one's is the person's choice
    /// (<see cref="ExecutableFacts.WithChoices"/>), and an unprotected one without it was not read.
    /// </summary>
    /// <param name="game">The game: its API, its executable and its candidates, its architecture.</param>
    /// <param name="runtimes">What it ships of its own.</param>
    /// <param name="gpu">The card and the driver; null when they could not be read.</param>
    /// <param name="files">What its folder says besides: an RE Engine game, its own DLSS beside the executable, a loader name free.</param>
    public static RouteFacts Of(GameEntry game, GameRuntimes runtimes, GpuFacts? gpu, RouteFolderFacts files)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(files);
        string executable = game.Executable ?? "";
        return new(game.Api, runtimes, files.IsReEngine, executable[(executable.LastIndexOfAny(['/', '\\']) + 1)..])
        {
            Bitness = game.Bitness,
            ArchitectureRead = !game.Protected && game.Bitness is not null,
            Executables = game.Candidates.Count,
            DlssBesideExecutable = files.DlssBesideExecutable,
            LoaderNameFree = files.LoaderNameFree,
            Gpu = gpu,
        };
    }
}

/// <summary>What a game's folder says about the routes, besides what the library keeps.</summary>
/// <param name="IsReEngine">A Capcom RE Engine game (see <see cref="Library.Protection.ReEngine.IsReEngine"/>).</param>
/// <param name="DlssBesideExecutable">A game's own <c>nvngx_dlss.dll</c> sits beside the executable.</param>
/// <param name="LoaderNameFree">The executable imports one of the multi-frame-generation loader's names that no other mod uses.</param>
public sealed record RouteFolderFacts(bool IsReEngine, bool DlssBesideExecutable, bool LoaderNameFree);

/// <summary>Which catalog components a route installs, from the route, the game, and the options only the install knows.</summary>
public static class RouteComponents
{
    // They close themselves the moment a ReShade DLL hooks Direct3D 11.
    private static readonly string[] _needDxvk = ["mgsvtpp.exe", "mgsvmgo.exe", "mgsgzs.exe"];

    /// <summary>
    /// The components <paramref name="route"/> installs, in catalog order: what every ReShade
    /// route installs, the route's own parts, DXVK when the game goes through it, REFramework on an RE
    /// Engine game. Only the settings the route shows for this game are read (<see cref="ShownSettings"/>).
    /// </summary>
    /// <param name="route">The chosen route.</param>
    /// <param name="facts">The game as read.</param>
    /// <param name="settings">The person's settings for the game.</param>
    public static IReadOnlyList<string> For(RouteId route, RouteFacts facts, RouteSettings settings)
    {
        ArgumentNullException.ThrowIfNull(facts);
        RouteSettings read = ShownSettings.Reset(route, facts, settings);
        HashSet<string> ids = [ComponentIds.Dlssnr, .. Own(route, facts, read.MotionProvider, read.SwapRemixRuntime)];
        if (IsReShadeRoute(route))
        {
            ids.UnionWith(EveryReShadeRoute(route, facts, read));
        }

        if (UsesDxvk(route, facts, read.Dxvk))
        {
            _ = ids.Add(ComponentIds.Dxvk);
        }

        if (facts.IsReEngine && route != RouteId.Remix)
        {
            _ = ids.Add(ComponentIds.Reframework);
        }

        return [.. ComponentCatalog.Ids.Where(ids.Contains)];
    }

    /// <summary>
    /// The provider the feeder gets: the choice, LumeniteFX's kernel by default; VORT on an OpenGL game, where
    /// LumeniteFX reads no motion. <see cref="MotionProvider.QuantMotion"/> stays itself: the install refuses it.
    /// </summary>
    public static MotionProvider ProviderFor(GraphicsApi api, MotionProvider? provider) => provider switch
    {
        null or MotionProvider.LumeniteKernel => api == GraphicsApi.OpenGL ? MotionProvider.Vort : MotionProvider.LumeniteKernel,
        { } chosen => chosen,
    };

    /// <summary>Whether the executable, by name or Windows path, is one known to close itself on a ReShade DLL, which DXVK serves by default.</summary>
    public static bool NeedsDxvk(string executableName)
    {
        ArgumentNullException.ThrowIfNull(executableName);
        return _needDxvk.Contains(executableName[(executableName.LastIndexOfAny(['/', '\\']) + 1)..], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Whether <paramref name="route"/> installs ReShade: every route but OptiScaler and remix.</summary>
    internal static bool IsReShadeRoute(RouteId route) => route is not (RouteId.OptiScaler or RouteId.Remix);

    /// <summary>Whether <paramref name="route"/> places <c>nvngx_dlss.dll</c> on the game: not upstream, uplift and remix, which run the network themselves; on OptiScaler only for a game redirected from FSR or XeSS, since one with DLSS keeps its own.</summary>
    internal static bool PlacesDlss(RouteId route, RouteFacts facts) => route switch
    {
        RouteId.Upstream or RouteId.Uplift or RouteId.Remix => false,
        RouteId.OptiScaler => !facts.Runtimes.OwnDlss,
        _ => true,
    };

    /// <summary>Whether the game can have ray reconstruction on <paramref name="route"/>: a ReShade route, on a game shipping <c>nvngx_dlssd.dll</c>.</summary>
    internal static bool OffersRayReconstruction(RouteId route, RouteFacts facts) => IsReShadeRoute(route) && facts.Runtimes.ShipsRayReconstruction;

    /// <summary>
    /// Whether the game can have RTX 40 multi-frame generation on <paramref name="route"/>: a ReShade route, an RTX 40
    /// card, the game's own DLSS frame generation, a loader name the executable imports and nothing else uses, on
    /// Direct3D 12 or Vulkan.
    /// </summary>
    internal static bool OffersMultiFrameGeneration(RouteId route, RouteFacts facts) =>
        IsReShadeRoute(route)
        && facts.Gpu?.Card?.Sm == CardNames.Rtx40
        && facts.Runtimes.ShipsFrameGeneration
        && facts.LoaderNameFree
        && facts.Api is GraphicsApi.DX12 or GraphicsApi.Vulkan;

    /// <summary>Whether <paramref name="route"/> places Krish's <c>renodx-dlss5</c> add-on, whose build the person can choose.</summary>
    internal static bool LoadsRenodx(RouteId route) => route is RouteId.Native or RouteId.Bridge or RouteId.Feeder or RouteId.Renodx;

    private static IEnumerable<string> Own(RouteId route, RouteFacts facts, MotionProvider? provider, bool swapRemixRuntime) => route switch
    {
        RouteId.Native => [ComponentIds.Renodx],
        RouteId.Upstream => [ComponentIds.Upstream],
        RouteId.Bridge => [ComponentIds.Bridge, ComponentIds.Renodx],
        RouteId.Feeder => [ComponentIds.Feeder, ComponentIds.Renodx, ComponentIds.ShaderHeaders, .. ProviderShaders(ProviderFor(facts.Api, provider))],
        RouteId.Standalone => [ComponentIds.Standalone, ComponentIds.ShaderHeaders, ComponentIds.Vort, ComponentIds.Dlssg],
        RouteId.Renodx => [ComponentIds.RenodxSf],
        RouteId.Uplift => [ComponentIds.Uplift],

        RouteId.OptiScaler => PlacesDlss(route, facts) ? [ComponentIds.OptiScaler, ComponentIds.Dlss] : [ComponentIds.OptiScaler],
        _ => swapRemixRuntime ? [ComponentIds.RemixRuntime] : [],
    };

    // Providers 0 and 1 are shaders the person installs, and 5 is the feeder's own. QuantMotion is refused
    // before anything is gathered, and installs nothing.
    private static IEnumerable<string> ProviderShaders(MotionProvider provider) => provider switch
    {
        MotionProvider.LumeniteKernel => [ComponentIds.Lumenite],
        MotionProvider.Vort => [ComponentIds.Vort],
        MotionProvider.Generic or MotionProvider.Launchpad or MotionProvider.BuiltIn or MotionProvider.QuantMotion => [],
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "not a motion-vector provider"),
    };

    // Ray reconstruction and multi-frame generation are read reset: on only where the game offers them.
    private static IEnumerable<string> EveryReShadeRoute(RouteId route, RouteFacts facts, RouteSettings read)
    {
        yield return ComponentIds.ReShade;
        if (PlacesDlss(route, facts) && !(read.KeepGameDlss && facts.DlssBesideExecutable))
        {
            yield return ComponentIds.Dlss;
        }

        if (read.RayReconstructionLabel is not null)
        {
            yield return ComponentIds.Dlssd;
        }

        if (read.MultiFrameGeneration)
        {
            yield return ComponentIds.Mfg;
            yield return ComponentIds.AsiLoader;
        }
    }

    /// <summary>
    /// Whether the game goes through DXVK on <paramref name="route"/> with the DXVK box at <paramref name="box"/>: on
    /// native, bridge and the feeder only, forced for Direct3D 9 and 8, by the box for 11. A game known to need it
    /// keeps it by default whatever API was set by hand: the detected one decides.
    /// </summary>
    internal static bool UsesDxvk(RouteId route, RouteFacts facts, bool? box) =>
        route is RouteId.Native or RouteId.Bridge or RouteId.Feeder
        && facts.Api switch
        {
            GraphicsApi.DX9 or GraphicsApi.DX8 => true,
            _ when NeedsDxvk(facts.ExecutableName) => box ?? true,
            GraphicsApi.DX11 => box ?? false,
            _ => false,
        };
}
