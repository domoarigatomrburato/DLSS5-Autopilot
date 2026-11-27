using System.Text.Json;
using System.Text.Json.Serialization;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>
/// Every per-game choice an install reads, typed: what routes.md's "What the person can override" lets the
/// person set beyond the route, the API, the architecture and the executable. <see langword="null"/> means
/// "the default" wherever a setting can be left blank. Which of them a route shows, and so reads, is
/// <see cref="ShownSettings"/>'s; a hidden one is reset to its default.
/// </summary>
public sealed record RouteSettings
{
    private const int MinModelResolution = 25;
    private const int MaxModelResolution = 100;
    private const int ModelResolutionStep = 5;
    private const int MaxVirtualKey = 0xFF;

    /// <summary>Every setting at its default.</summary>
    public static RouteSettings Default { get; } = new();

    /// <summary>The name ReShade loads as; <see langword="null"/> for the name the API gives.</summary>
    public ReShadeProxy? ReShadeLoadsAs { get; init; }

    /// <summary>The name OptiScaler loads as; <see langword="null"/> for the first free name.</summary>
    public OptiScalerProxy? OptiScalerLoadsAs { get; init; }

    /// <summary>The OptiScaler build; <see langword="null"/> for the default build.</summary>
    public OptiScalerBuildKey? OptiScalerBuild { get; init; }

    /// <summary>OptiScaler's preset; <see langword="null"/> when not set (the build's own).</summary>
    public NeuralPreset? OptiScalerPreset { get; init; }

    /// <summary>OptiScaler's style; <see langword="null"/> when not set.</summary>
    public NeuralStyle? OptiScalerStyle { get; init; }

    /// <summary>OptiScaler's passes, for a build that runs before super resolution; <see langword="null"/> when not set.</summary>
    public NeuralPasses? OptiScalerPasses { get; init; }

    /// <summary>The model-resolution dial in percent, 25 to 100 (OptiScaler, or the feeder's work resolution); <see langword="null"/> for the tool's default.</summary>
    public int? ModelResolution { get; init; }

    /// <summary>OptiScaler's frame generation (the bundled FSR 3.1 libraries), on D3D12.</summary>
    public bool FrameGeneration { get; init; }

    /// <summary>The frame generation files the person downloaded, placed beside OptiScaler; <see langword="null"/> for none.</summary>
    public OwnFrameGeneration? OwnFrameGeneration { get; init; }

    /// <summary>
    /// The feeder's motion-vector provider; <see langword="null"/> for the default (see <see cref="RouteComponents.ProviderFor"/>).
    /// <see cref="MotionProvider.QuantMotion"/> is never offered: an install that names it is refused.
    /// </summary>
    public MotionProvider? MotionProvider { get; init; }

    /// <summary>The feeder's DLSS preset; <see langword="null"/> for the default.</summary>
    public DlssPreset? FeederPreset { get; init; }

    /// <summary>The feeder's HDR mode; <see langword="null"/> for auto.</summary>
    public FeederHdr? FeederHdr { get; init; }

    /// <summary>The feeder build; <see langword="null"/> for the newest release.</summary>
    public FeederChoice? FeederBuild { get; init; }

    /// <summary>The feeder's 64-bit helper process, on a 64-bit game.</summary>
    public bool HelperProcess { get; init; }

    /// <summary>The DXVK box on a Direct3D 11 game; <see langword="null"/> for its default, on for a game known to need it.</summary>
    public bool? Dxvk { get; init; }

    /// <summary>The <c>renodx-dlss5</c> build's label; <see langword="null"/> for the pins' choice. <see cref="RenodxFile"/> wins over it.</summary>
    public string? RenodxLabel { get; init; }

    /// <summary>"Use my file": the full path of a 64-bit <c>renodx-dlss5</c> add-on of the person's own; <see langword="null"/> for none.</summary>
    public string? RenodxFile { get; init; }

    /// <summary>The pinned <c>nvngx_dlssnr.dll</c> build's label; <see langword="null"/> for auto, the newest that runs on the card.</summary>
    public string? DlssnrLabel { get; init; }

    /// <summary>The pinned <c>nvngx_dlss.dll</c> build's label; <see langword="null"/> for NVIDIA's newest.</summary>
    public string? DlssLabel { get; init; }

    /// <summary>Keep the game's own <c>nvngx_dlss.dll</c>; on by default.</summary>
    public bool KeepGameDlss { get; init; } = true;

    /// <summary>The ray-reconstruction build swapped over the game's own <c>nvngx_dlssd.dll</c>; <see langword="null"/> for no swap.</summary>
    public string? RayReconstructionLabel { get; init; }

    /// <summary>The RTX 40 multi-frame-generation unlock.</summary>
    public bool MultiFrameGeneration { get; init; }

    /// <summary>The Windows virtual-key code of the key that opens the overlay; <see langword="null"/> for the route's default.</summary>
    public int? OverlayKey { get; init; }

    /// <summary>Also register ReShade's OpenXR layer, for a VR headset.</summary>
    public bool VrHeadset { get; init; }

    /// <summary>"Swap the Remix runtime": the mod's runtime replaced with a community one.</summary>
    public bool SwapRemixRuntime { get; init; }

    /// <summary>"Use the NVIDIA card": the Windows graphics preference, on a two-GPU PC; on by default.</summary>
    public bool UseNvidiaCard { get; init; } = true;

    /// <summary>
    /// Whether every setting holds a value the person can choose: a feeder build whose tag goes with its pick, a
    /// model resolution of 25 to 100 percent in steps of 5 (app.md, "work area"), an overlay key that is a Windows
    /// virtual key. A record whose settings are not is the wrong shape.
    /// </summary>
    internal bool IsValid =>
        FeederBuild is null or { IsWhole: true }
        && ModelResolution is null or (>= MinModelResolution and <= MaxModelResolution)
        && ModelResolution.GetValueOrDefault() % ModelResolutionStep == 0
        && OverlayKey is null or (> 0 and < MaxVirtualKey);

    /// <summary>The feeder build the install resolves: the choice, else the newest release.</summary>
    [JsonIgnore]
    public FeederChoice Feeder => FeederBuild ?? FeederChoice.NewestRelease;

    /// <summary>The OptiScaler build the install resolves: the choice, else the default build.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The choice names no build.</exception>
    [JsonIgnore]
    public OptiScalerBuild OptiScaler => OptiScalerBuild is { } key ? key.Build() : Components.Sources.OptiScalerBuild.Default;
}

/// <summary>The names ReShade can load as on a route that places it beside the executable.</summary>
public enum ReShadeProxy
{
    /// <summary><c>dxgi.dll</c>.</summary>
    Dxgi,

    /// <summary><c>d3d11.dll</c>.</summary>
    D3d11,

    /// <summary><c>d3d12.dll</c>.</summary>
    D3d12,

    /// <summary><c>d3d10.dll</c>.</summary>
    D3d10,

    /// <summary><c>opengl32.dll</c>.</summary>
    Opengl32,
}

/// <summary>The names OptiScaler can load as, in the order a blank choice tries them.</summary>
public enum OptiScalerProxy
{
    /// <summary><c>dxgi.dll</c>.</summary>
    Dxgi,

    /// <summary><c>winmm.dll</c>.</summary>
    Winmm,

    /// <summary><c>version.dll</c>.</summary>
    Version,

    /// <summary><c>dbghelp.dll</c>.</summary>
    Dbghelp,

    /// <summary><c>d3d12.dll</c>.</summary>
    D3d12,

    /// <summary><c>wininet.dll</c>.</summary>
    Wininet,

    /// <summary><c>winhttp.dll</c>.</summary>
    Winhttp,
}

/// <summary>The OptiScaler builds besides the default one (components.md, "OptiScaler builds").</summary>
public enum OptiScalerBuildKey
{
    /// <summary>y4my4my4m's development builds.</summary>
    Y4my4my4m,

    /// <summary>wilsjo2's pass before super resolution.</summary>
    Wilsjo2,

    /// <summary>wilsjo2's RTX 40 MFG package; refused on any card but RTX 40.</summary>
    Wilsjo2Mfg,

    /// <summary>Janblade's fork of wilsjo2's.</summary>
    Janblade,

    /// <summary>jlrouzies' fork of wilsjo2's.</summary>
    Jlrouzies,
}

/// <summary>OptiScaler's <c>[DlssNr] Preset</c>.</summary>
public enum NeuralPreset
{
    /// <summary>0, the build's default.</summary>
    Default,

    /// <summary>Preset 1.</summary>
    Preset1,

    /// <summary>Preset 2.</summary>
    Preset2,

    /// <summary>Preset 3.</summary>
    Preset3,
}

/// <summary>OptiScaler's <c>[DlssNr] Style</c>.</summary>
public enum NeuralStyle
{
    /// <summary>0.</summary>
    Standard,

    /// <summary>1.</summary>
    Natural,

    /// <summary>2.</summary>
    Cinematic,
}

/// <summary>OptiScaler's <c>[DlssNr] Passes</c>, on the builds that run before super resolution.</summary>
public enum NeuralPasses
{
    /// <summary>Auto, written as 1.</summary>
    Auto,

    /// <summary>Two passes.</summary>
    Two,

    /// <summary>Three passes.</summary>
    Three,
}

/// <summary>The feeder's DLSS preset (<c>preset</c> in <c>dlss5-feed.cfg</c>), by the number it writes.</summary>
public enum DlssPreset
{
    /// <summary>0, the default.</summary>
    Default = 0,

    /// <summary>5, legacy CNN E.</summary>
    E = 5,

    /// <summary>6, legacy CNN F.</summary>
    F = 6,

    /// <summary>10, transformer J.</summary>
    J = 10,

    /// <summary>11, transformer K.</summary>
    K = 11,

    /// <summary>12, transformer L.</summary>
    L = 12,

    /// <summary>13, transformer M.</summary>
    M = 13,
}

/// <summary>The feeder's HDR mode (<c>hdr</c> in <c>dlss5-feed.cfg</c>).</summary>
public enum FeederHdr
{
    /// <summary>-1, auto.</summary>
    Auto,

    /// <summary>0.</summary>
    Sdr,

    /// <summary>1.</summary>
    Hdr,
}

/// <summary>The frame generation files a person can download and have placed beside OptiScaler (routes.md, "Frame generation").</summary>
public enum OwnFrameGeneration
{
    /// <summary>dlssg_for_sm86 as <c>version.dll</c>, with <c>dlssg_sm86.ini</c>.</summary>
    Sm86,

    /// <summary>dlssg_for_sm86 as <c>winmm.dll</c>, with <c>dlssg_sm86.ini</c>.</summary>
    Sm86Winmm,

    /// <summary>dlssg_for_sm86 as <c>dxgi.dll</c>, with <c>dlssg_sm86.ini</c>.</summary>
    Sm86Dxgi,

    /// <summary>dlssg_for_sm86 as <c>d3d12.dll</c>, with <c>dlssg_sm86.ini</c>.</summary>
    Sm86D3d12,

    /// <summary>dlssg_for_sm86 as <c>dbghelp.dll</c>, with <c>dlssg_sm86.ini</c>.</summary>
    Sm86Dbghelp,

    /// <summary>DLSS Enabler, headless: its <c>version.dll</c> as <c>dlss-enabler-headless.dll</c>.</summary>
    Enabler,
}

/// <summary>The file names each setting's choices stand for.</summary>
public static class SettingNames
{
    /// <summary>The file ReShade loads as.</summary>
    public static string FileName(this ReShadeProxy proxy) => proxy switch
    {
        ReShadeProxy.Dxgi => KnownFiles.Dxgi,
        ReShadeProxy.D3d11 => KnownFiles.D3d11,
        ReShadeProxy.D3d12 => KnownFiles.D3d12,
        ReShadeProxy.D3d10 => KnownFiles.D3d10,
        ReShadeProxy.Opengl32 => KnownFiles.OpenGL,
        _ => throw new ArgumentOutOfRangeException(nameof(proxy), proxy, "not a ReShade proxy name"),
    };

    /// <summary>The file OptiScaler loads as.</summary>
    public static string FileName(this OptiScalerProxy proxy) => proxy switch
    {
        OptiScalerProxy.Dxgi => KnownFiles.Dxgi,
        OptiScalerProxy.Winmm => KnownFiles.Winmm,
        OptiScalerProxy.Version => KnownFiles.Version,
        OptiScalerProxy.Dbghelp => KnownFiles.Dbghelp,
        OptiScalerProxy.D3d12 => KnownFiles.D3d12,
        OptiScalerProxy.Wininet => KnownFiles.Wininet,
        OptiScalerProxy.Winhttp => KnownFiles.Winhttp,
        _ => throw new ArgumentOutOfRangeException(nameof(proxy), proxy, "not an OptiScaler proxy name"),
    };

    /// <summary>The names the files go in under, beside the executable, the DLL first.</summary>
    public static IReadOnlyList<string> FileNames(this OwnFrameGeneration files) => files switch
    {
        OwnFrameGeneration.Sm86 => [KnownFiles.Version, KnownFiles.Sm86Ini],
        OwnFrameGeneration.Sm86Winmm => [KnownFiles.Winmm, KnownFiles.Sm86Ini],
        OwnFrameGeneration.Sm86Dxgi => [KnownFiles.Dxgi, KnownFiles.Sm86Ini],
        OwnFrameGeneration.Sm86D3d12 => [KnownFiles.D3d12, KnownFiles.Sm86Ini],
        OwnFrameGeneration.Sm86Dbghelp => [KnownFiles.Dbghelp, KnownFiles.Sm86Ini],
        OwnFrameGeneration.Enabler => [KnownFiles.DlssEnablerHeadless],
        _ => throw new ArgumentOutOfRangeException(nameof(files), files, "not a set of frame generation files"),
    };

    /// <summary>
    /// The folder of the tool's own copy of <paramref name="files"/>, under its store of the person's frame generation
    /// files: the set's name as the record spells it (<c>sm86-winmm</c>).
    /// </summary>
    public static string StoreFolder(this OwnFrameGeneration files) => Kebab(files);

    /// <summary>The project's name, as the person reads it.</summary>
    public static string Label(this OwnFrameGeneration files) => files == OwnFrameGeneration.Enabler ? "DLSS Enabler" : "dlssg_for_sm86";

    /// <summary>The build <paramref name="key"/> names: the one whose key is its name as the record spells it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> names no build.</exception>
    public static OptiScalerBuild Build(this OptiScalerBuildKey key) =>
        OptiScalerBuild.ByKey(Kebab(key)) ?? throw new ArgumentOutOfRangeException(nameof(key), key, "not an OptiScaler build");

    // A choice's name as the records spell it (InstallJsonContext's kebab-case names).
    private static string Kebab<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
}
