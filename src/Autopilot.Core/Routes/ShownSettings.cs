using Autopilot.Core.Executables;

namespace Autopilot.Core.Routes;

/// <summary>The settings a game's page can show, one per row of routes.md's "Per-route settings the person sees".</summary>
public enum RouteSetting
{
    /// <summary>The route.</summary>
    Route,

    /// <summary>The architecture.</summary>
    Architecture,

    /// <summary>The executable.</summary>
    Executable,

    /// <summary>The graphics API.</summary>
    GraphicsApi,

    /// <summary>The name OptiScaler loads as.</summary>
    OptiScalerLoadsAs,

    /// <summary>The OptiScaler build.</summary>
    OptiScalerBuild,

    /// <summary>OptiScaler's preset.</summary>
    OptiScalerPreset,

    /// <summary>OptiScaler's style.</summary>
    OptiScalerStyle,

    /// <summary>OptiScaler's passes.</summary>
    OptiScalerPasses,

    /// <summary>The model-resolution dial.</summary>
    ModelResolution,

    /// <summary>OptiScaler's frame generation.</summary>
    FrameGeneration,

    /// <summary>The frame generation files the person downloaded.</summary>
    OwnFrameGeneration,

    /// <summary>Forgetting those files.</summary>
    ForgetOwnFrameGeneration,

    /// <summary>The feeder's motion-vector provider.</summary>
    MotionProvider,

    /// <summary>The feeder's preset.</summary>
    FeederPreset,

    /// <summary>The feeder's HDR mode.</summary>
    FeederHdr,

    /// <summary>The feeder build.</summary>
    FeederBuild,

    /// <summary>The 64-bit helper process.</summary>
    HelperProcess,

    /// <summary>The name ReShade loads as.</summary>
    ReShadeLoadsAs,

    /// <summary>The <c>renodx-dlss5</c> build.</summary>
    RenodxBuild,

    /// <summary>The <c>nvngx_dlssnr.dll</c> build.</summary>
    DlssnrBuild,

    /// <summary>The <c>nvngx_dlss.dll</c> build.</summary>
    DlssBuild,

    /// <summary>Keeping the game's own <c>nvngx_dlss.dll</c>.</summary>
    KeepGameDlss,

    /// <summary>The ray-reconstruction build.</summary>
    RayReconstruction,

    /// <summary>The DXVK box.</summary>
    Dxvk,

    /// <summary>RTX 40 multi-frame generation.</summary>
    MultiFrameGeneration,

    /// <summary>The overlay key.</summary>
    OverlayKey,

    /// <summary>The VR headset.</summary>
    VrHeadset,

    /// <summary>Swapping the Remix runtime.</summary>
    SwapRemixRuntime,

    /// <summary>The Windows graphics preference.</summary>
    GraphicsPreference,
}

/// <summary>
/// The one rule set for which settings a route shows for a game: the page, the install and the reset on a
/// route change all read it. A hidden setting is not read by the install and is back at its default.
/// </summary>
public static class ShownSettings
{
    // A setting the person cannot have changed from its default reads as itself; one shown is never reset.
    private static readonly Row[] _rows =
    [
        Row.Kept(RouteSetting.Route, _ => true),
        Row.Kept(RouteSetting.Architecture, game => !game.Facts.ArchitectureRead),
        Row.Kept(RouteSetting.Executable, game => game.Facts.Executables > 1),
        Row.Kept(RouteSetting.GraphicsApi, _ => true),
        Row.Hidden(RouteSetting.OptiScalerLoadsAs, OnOptiScaler, settings => settings.OptiScalerLoadsAs, (settings, value) => settings with { OptiScalerLoadsAs = value }),
        Row.Hidden(RouteSetting.OptiScalerBuild, OnOptiScaler, settings => settings.OptiScalerBuild, (settings, value) => settings with { OptiScalerBuild = value }),
        Row.Hidden(RouteSetting.OptiScalerPreset, OnOptiScaler, settings => settings.OptiScalerPreset, (settings, value) => settings with { OptiScalerPreset = value }),
        Row.Hidden(RouteSetting.OptiScalerStyle, OnOptiScaler, settings => settings.OptiScalerStyle, (settings, value) => settings with { OptiScalerStyle = value }),
        Row.Hidden(RouteSetting.OptiScalerPasses, game => OnOptiScaler(game) && game.Settings.OptiScaler.RunsBeforeSuperResolution, settings => settings.OptiScalerPasses, (settings, value) => settings with { OptiScalerPasses = value }),
        Row.Hidden(RouteSetting.ModelResolution, ShowsModelResolution, settings => settings.ModelResolution, (settings, value) => settings with { ModelResolution = value }),
        Row.Hidden(RouteSetting.FrameGeneration, OptiScalerOnD3d12, settings => settings.FrameGeneration, (settings, value) => settings with { FrameGeneration = value }),
        Row.Hidden(RouteSetting.OwnFrameGeneration, OptiScalerOnD3d12, settings => settings.OwnFrameGeneration, (settings, value) => settings with { OwnFrameGeneration = value }),
        Row.Kept(RouteSetting.ForgetOwnFrameGeneration, game => OptiScalerOnD3d12(game) && game.Settings.OwnFrameGeneration is not null),
        Row.Hidden(RouteSetting.MotionProvider, OnFeeder, settings => settings.MotionProvider, (settings, value) => settings with { MotionProvider = value }),
        Row.Hidden(RouteSetting.FeederPreset, OnFeeder, settings => settings.FeederPreset, (settings, value) => settings with { FeederPreset = value }),
        Row.Hidden(RouteSetting.FeederHdr, OnFeeder, settings => settings.FeederHdr, (settings, value) => settings with { FeederHdr = value }),
        Row.Hidden(RouteSetting.FeederBuild, OnFeeder, settings => settings.FeederBuild, (settings, value) => settings with { FeederBuild = value }),
        Row.Hidden(RouteSetting.HelperProcess, game => OnFeeder(game) && game.Facts.Bitness == Bitness.Bits64, settings => settings.HelperProcess, (settings, value) => settings with { HelperProcess = value }),
        Row.Hidden(RouteSetting.ReShadeLoadsAs, ShowsReShadeLoadsAs, settings => settings.ReShadeLoadsAs, (settings, value) => settings with { ReShadeLoadsAs = value }),
        Row.Hidden(RouteSetting.RenodxBuild, game => RouteComponents.LoadsRenodx(game.Route), settings => (settings.RenodxLabel, settings.RenodxFile), (settings, value) => settings with { RenodxLabel = value.RenodxLabel, RenodxFile = value.RenodxFile }),
        Row.Hidden(RouteSetting.DlssnrBuild, _ => true, settings => settings.DlssnrLabel, (settings, value) => settings with { DlssnrLabel = value }),
        Row.Hidden(RouteSetting.DlssBuild, ShowsDlssBuild, settings => settings.DlssLabel, (settings, value) => settings with { DlssLabel = value }),
        Row.Hidden(RouteSetting.KeepGameDlss, game => RouteComponents.PlacesDlss(game.Route, game.Facts) && game.Facts.Bitness != Bitness.Bits32, settings => settings.KeepGameDlss, (settings, value) => settings with { KeepGameDlss = value }),
        Row.Hidden(RouteSetting.RayReconstruction, game => RouteComponents.OffersRayReconstruction(game.Route, game.Facts), settings => settings.RayReconstructionLabel, (settings, value) => settings with { RayReconstructionLabel = value }),
        Row.Hidden(RouteSetting.Dxvk, ShowsDxvk, settings => settings.Dxvk, (settings, value) => settings with { Dxvk = value }),
        Row.Hidden(RouteSetting.MultiFrameGeneration, game => RouteComponents.OffersMultiFrameGeneration(game.Route, game.Facts), settings => settings.MultiFrameGeneration, (settings, value) => settings with { MultiFrameGeneration = value }),
        Row.Hidden(RouteSetting.OverlayKey, game => game.Route != RouteId.Remix, settings => settings.OverlayKey, (settings, value) => settings with { OverlayKey = value }),
        Row.Hidden(RouteSetting.VrHeadset, game => RouteComponents.IsReShadeRoute(game.Route) && game.Facts.Bitness == Bitness.Bits64, settings => settings.VrHeadset, (settings, value) => settings with { VrHeadset = value }),
        Row.Hidden(RouteSetting.SwapRemixRuntime, game => game.Route == RouteId.Remix, settings => settings.SwapRemixRuntime, (settings, value) => settings with { SwapRemixRuntime = value }),
        Row.Hidden(RouteSetting.GraphicsPreference, game => game.Facts.Gpu?.TwoGpus == true, settings => settings.UseNvidiaCard, (settings, value) => settings with { UseNvidiaCard = value }),
    ];

    /// <summary>The settings the page shows for <paramref name="route"/> on the game in <paramref name="facts"/>, with <paramref name="settings"/> as they stand once reset.</summary>
    public static IReadOnlySet<RouteSetting> For(RouteId route, RouteFacts facts, RouteSettings settings)
    {
        Game game = new(route, facts, Reset(route, facts, settings));
        return _rows.Where(row => row.Shown(game)).Select(row => row.Setting).ToHashSet();
    }

    /// <summary>
    /// <paramref name="settings"/> with every setting <paramref name="route"/> hides for this game back at its
    /// default: what the install reads, and what a route change leaves.
    /// </summary>
    public static RouteSettings Reset(RouteId route, RouteFacts facts, RouteSettings settings)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(settings);

        // A reset can hide another setting (DXVK back on by default hides the name ReShade loads as): until nothing moves.
        RouteSettings current = settings;
        while (true)
        {
            Game game = new(route, facts, current);
            RouteSettings next = _rows.Where(row => !row.Shown(game)).Aggregate(current, (reset, row) => row.Reset(reset));
            if (ReferenceEquals(next, current))
            {
                return current;
            }

            current = next;
        }
    }

    private static bool OnOptiScaler(Game game) => game.Route == RouteId.OptiScaler;

    private static bool OnFeeder(Game game) => game.Route == RouteId.Feeder;

    private static bool OptiScalerOnD3d12(Game game) => OnOptiScaler(game) && game.Facts.Api == Graphics.GraphicsApi.DX12;

    // The feeder's work resolution: a 64-bit Direct3D 11 game drawing itself, not through DXVK, not in the helper.
    private static bool ShowsModelResolution(Game game) =>
        OnOptiScaler(game)
        || (OnFeeder(game)
            && game.Facts.Api == Graphics.GraphicsApi.DX11
            && game.Facts.Bitness == Bitness.Bits64
            && !RouteComponents.UsesDxvk(game.Route, game.Facts, game.Settings.Dxvk)
            && !game.Settings.HelperProcess);

    // D3D8 and D3D9 load it under their own names, Vulkan as a layer, and DXVK makes the game a Vulkan one.
    private static bool ShowsReShadeLoadsAs(Game game) =>
        RouteComponents.IsReShadeRoute(game.Route)
        && game.Facts.Api is not (Graphics.GraphicsApi.Vulkan or Graphics.GraphicsApi.DX9 or Graphics.GraphicsApi.DX8)
        && !RouteComponents.UsesDxvk(game.Route, game.Facts, game.Settings.Dxvk);

    private static bool ShowsDlssBuild(Game game) =>
        RouteComponents.PlacesDlss(game.Route, game.Facts) && !(game.Settings.KeepGameDlss && game.Facts.DlssBesideExecutable);

    private static bool ShowsDxvk(Game game) =>
        game.Route is RouteId.Native or RouteId.Bridge or RouteId.Feeder
        && (game.Facts.Api == Graphics.GraphicsApi.DX11 || RouteComponents.NeedsDxvk(game.Facts.ExecutableName));

    private sealed record Game(RouteId Route, RouteFacts Facts, RouteSettings Settings);

    // A row of the table: when its setting shows, and how a hidden one goes back to its default (the same settings
    // back when it already is).
    private sealed record Row(RouteSetting Setting, Func<Game, bool> Shown, Func<RouteSettings, RouteSettings> Reset)
    {
        // A row with nothing to reset: a fact of the game, or an action.
        public static Row Kept(RouteSetting setting, Func<Game, bool> shown) => new(setting, shown, settings => settings);

        public static Row Hidden<T>(RouteSetting setting, Func<Game, bool> shown, Func<RouteSettings, T> read, Func<RouteSettings, T, RouteSettings> write)
        {
            T byDefault = read(RouteSettings.Default);
            return new(setting, shown, settings => EqualityComparer<T>.Default.Equals(read(settings), byDefault) ? settings : write(settings, byDefault));
        }
    }
}
