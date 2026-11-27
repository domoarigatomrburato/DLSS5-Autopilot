using System.Collections.Frozen;

namespace Autopilot.Core.Graphics;

/// <summary>
/// The file names the tool knows by heart, in one place: its own add-ons, other DLSS tools' files,
/// the NGX runtimes and the DLLs whose contents say nothing about a game. Every set ignores case.
/// </summary>
internal static class KnownFiles
{
    public const string InstallRecord = "invidia-claritatis.json";

    /// <summary>The game's own file the tool replaced or removed, kept beside it; restored by uninstall.</summary>
    public const string BackupSuffix = ".invidia-claritatis-backup";

    /// <summary>The game's <c>d3dcompiler_47.dll</c>, renamed so Windows' newer one loads; restored by uninstall as a backup.</summary>
    public const string DlssOffSuffix = ".dlss5-off";

    /// <summary>A file of the previous install, kept beside itself until the install over it commits; no longer loaded by ReShade.</summary>
    public const string HeldSuffix = ".invidia-claritatis-held";

    /// <summary>An old original replaced by a newer one the game shipped; no restore ever touches it (a taken name gains <c>.2</c>, <c>.3</c> ...).</summary>
    public const string SupersededSuffix = ".invidia-claritatis-superseded";

    public const string AgilityFolder = "D3D12";

    public const string AgilityCore = "D3D12Core.dll";

    public const string Dlss = "nvngx_dlss.dll";

    public const string FrameGeneration = "nvngx_dlssg.dll";

    public const string RayReconstruction = "nvngx_dlssd.dll";

    /// <summary>NVIDIA's neural-rendering runtime.</summary>
    public const string NeuralRendering = "nvngx_dlssnr.dll";

    /// <summary>The Remix runtime's own NGX runtime, beside its <c>d3d9.dll</c>.</summary>
    public const string RemixNgx = "remix_nvngx.dll";

    public const string UnityPlayer = "UnityPlayer.dll";

    public const string D3d12 = "d3d12.dll";

    public const string D3d11 = "d3d11.dll";

    public const string D3d10 = "d3d10.dll";

    public const string Dxgi = "dxgi.dll";

    public const string Vulkan = "vulkan-1.dll";

    public const string OpenGL = "opengl32.dll";

    public const string D3d9 = "d3d9.dll";

    public const string D3d8 = "d3d8.dll";

    /// <summary>The loader name ASI loaders and REFramework take.</summary>
    public const string Dinput8 = "dinput8.dll";

    /// <summary>Windows' multimedia library, a name proxies load as.</summary>
    public const string Winmm = "winmm.dll";

    /// <summary>Windows' version library, a name proxies load as.</summary>
    public const string Version = "version.dll";

    /// <summary>Windows' debug-help library, a name proxies load as.</summary>
    public const string Dbghelp = "dbghelp.dll";

    /// <summary>Windows' internet library, a name proxies load as.</summary>
    public const string Wininet = "wininet.dll";

    /// <summary>Windows' HTTP library, a name proxies load as.</summary>
    public const string Winhttp = "winhttp.dll";

    /// <summary>DLSS Enabler's headless DLL, as the person's own frame generation files name it.</summary>
    public const string DlssEnablerHeadless = "dlss-enabler-headless.dll";

    /// <summary>dlssg_for_sm86's settings file.</summary>
    public const string Sm86Ini = "dlssg_sm86.ini";

    /// <summary>The proxy DLLs DXVK ships per architecture folder.</summary>
    public static readonly IReadOnlyList<string> DxvkProxies = [Dxgi, D3d11, D3d9, D3d8];

    /// <summary>Direct3D 10's DLLs, any of which a Direct3D 10 game may load.</summary>
    public static readonly IReadOnlyList<string> D3d10Family = [D3d10, "d3d10_1.dll", "d3d10core.dll"];

    /// <summary>The game's own NGX runtimes, in the order they are named as evidence.</summary>
    public static readonly IReadOnlyList<string> NgxRuntimes = [Dlss, FrameGeneration, RayReconstruction];

    /// <summary>Streamline's frame-generation plugin.</summary>
    public const string StreamlineFrameGeneration = "sl.dlss_g.dll";

    /// <summary>Streamline, NVIDIA's plugin layer: a game that ships any of these ships DLSS.</summary>
    public static readonly IReadOnlyList<string> Streamline = ["sl.interposer.dll", "sl.dlss.dll", "sl.common.dll", StreamlineFrameGeneration, "sl.reflex.dll"];

    /// <summary>A game's own DLSS runtime, renamed by a person to switch it off.</summary>
    public static readonly IReadOnlyList<string> RenamedDlss = ["nvngx_dlss.dlsss", "nvngx_dlss.dll.bak", "_nvngx.dll"];

    /// <summary>The runtime DLLs the AMD FSR 2/3 SDKs ship under, one per line: the secret scan reads two "..._api_..." names on a line as a key and its value.</summary>
    public static readonly IReadOnlyList<string> FsrRuntimes =
    [
        "ffx_fsr2_api_x64.dll",
        "ffx_fsr2_api_dx12_x64.dll",
        "ffx_fsr2_api_vk_x64.dll",
        "ffx_fsr3upscaler_x64.dll",
        "ffx_fsr3_x64.dll",
        "amd_fidelityfx_dx12.dll",
        "amd_fidelityfx_vk.dll",
        "ffx_backend_dx12_x64.dll",
    ];

    /// <summary>The runtime DLLs the Intel XeSS SDK ships under.</summary>
    public static readonly IReadOnlyList<string> XessRuntimes = ["libxess.dll", "libxess_dx11.dll", "libxess_fg.dll"];

    /// <summary>The NGX runtimes that run on Direct3D 12 only.</summary>
    public static readonly IReadOnlyList<string> D3d12OnlyRuntimes = [FrameGeneration, RayReconstruction];

    /// <summary>Renderer DLL names a game loads, ranked: a Direct3D name outranks OpenGL and Vulkan.</summary>
    public static readonly IReadOnlyList<(string Name, GraphicsApi Api)> Renderers =
    [
        (D3d12, GraphicsApi.DX12),
        (D3d11, GraphicsApi.DX11),
        (D3d10, GraphicsApi.DX10),
        (Dxgi, GraphicsApi.Dxgi),
        (Vulkan, GraphicsApi.Vulkan),
        (OpenGL, GraphicsApi.OpenGL),
        (D3d9, GraphicsApi.DX9),
        (D3d8, GraphicsApi.DX8),
    ];

    /// <summary>Names engines that can drive several backends carry without drawing with them.</summary>
    public static readonly FrozenSet<string> Ambiguous = Set(OpenGL, Vulkan);

    /// <summary>The add-ons this tool installs.</summary>
    public static readonly FrozenSet<string> OurAddons = Set(
        "dlss5-feed.addon64",
        "dlss5-feed.addon32",
        "dlss5-bridge.addon64",
        "renodx-dlss5.addon64",
        "renodx-dlss.addon64",
        "nvngx.dll.addon64",
        "standalone-dlssnr.addon64",
        "rtx40mfg-ui.addon64",
        "dlss5-feed-helper.addon64",
        "dlss5-dx11-bridge.addon64");

    /// <summary>Files that mean another DLSS or NGX tool is installed in the folder.</summary>
    public static readonly FrozenSet<string> ForeignNgxFiles = Set(
        "optiscaler.ini",
        "nvngx.ini",
        "fakenvapi.ini",
        "dlss-enabler.dll",
        "dlss-enabler-upscaler.dll",
        "nvngx-wrapper.dll",
        "dlssg_to_fsr3_amd_is_better.dll",
        "dlssg_to_fsr3.ini",
        "nvngx.dll_dlssnr.dll",
        Sm86Ini,
        "nvngx_dlssnr.ini",
        "gitc-uplift-helper64.exe",
        "uplift-helper64.exe");

    /// <summary>Words that make a ReShade add-on another DLSS tool's ("dlss" covers dlssg and dlssd, "ngx" nvngx).</summary>
    public static readonly IReadOnlyList<string> NgxAddonWords = ["dlss", "ngx", "swapper"];

    /// <summary>The add-on extensions pe.md's rule for another tool's NGX add-on counts.</summary>
    public static readonly IReadOnlyList<string> AddonExtensions = [".addon64", ".addon32"];

    /// <summary>Every extension ReShade loads an add-on from: <see cref="AddonExtensions"/> and the plain <c>.addon</c>.</summary>
    public static readonly IReadOnlyList<string> AnyAddonExtensions = [.. AddonExtensions, ".addon"];

    /// <summary>
    /// DLLs beside a game whose imports and strings say nothing about it: proxies (DXVK, ReShade,
    /// wrappers) and the DLSS, OptiScaler, Streamline, XeSS and FidelityFX files routes and games ship.
    /// </summary>
    public static readonly FrozenSet<string> NotTheGame = Set(
    [
        .. Renderers.Select(renderer => renderer.Name),
        .. Streamline,
        .. XessRuntimes,
        "reshade32.dll", "reshade64.dll", "reshade.dll", "ddraw.dll", Dinput8, Winmm,
        Version, Dbghelp, Winhttp, Wininet, "d3dcompiler_47.dll",
        NeuralRendering, Dlss, RayReconstruction, FrameGeneration, "_nvngx.dll", "nvngx-wrapper.dll",
        RemixNgx, "optiscaler.dll", "dlss5-bridge.dll", "dlss-enabler.dll",
        DlssEnablerHeadless, "rtx40mfgcore.dll", "sl.dlss_d.dll", "sl.pcl.dll", "libxell.dll", "amd_fidelityfx_dx12.dll",
        "amd_fidelityfx_vk.dll", "amd_fidelityfx_upscaler_dx12.dll",
        "amd_fidelityfx_framegeneration_dx12.dll", "amd_fidelityfx_loader_dx12.dll",
        "ffx_fsr2_api_x64.dll", "ffx_fsr2_api_dx12_x64.dll", "ffx_fsr2_api_vk_x64.dll",
        "ffx_fsr3upscaler_x64.dll", "ffx_backend_dx12_x64.dll", "ffx_backend_vk_x64.dll",
    ]);

    /// <summary>Middleware that names every graphics API it instruments, whichever one the game uses.</summary>
    public static readonly FrozenSet<string> NamesEveryApi = Set(
        "gfsdk_aftermath_lib.x64.dll",
        "gfsdk_aftermath_lib.dll",
        "amd_ags_x64.dll",
        "amd_ags_x86.dll",
        "eossdk-win64-shipping.dll",
        "eossdk-win32-shipping.dll");

    /// <summary>Whether a file in a game's folder belongs to another DLSS or NGX tool (pe.md: an add-on only when its name says so).</summary>
    public static bool IsForeignNgx(string name) =>
        ForeignNgxFiles.Contains(name)
        || (EndsWithAny(name, AddonExtensions)
            && !OurAddons.Contains(name)
            && NgxAddonWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Whether a file in a game's folder is another hook, which the install warns about (routes.md): another DLSS
    /// or NGX tool's file, or any ReShade add-on that is not ours.
    /// </summary>
    public static bool IsAnotherHook(string name) =>
        ForeignNgxFiles.Contains(name) || (IsAddon(name) && !OurAddons.Contains(name));

    /// <summary>Whether <paramref name="name"/> is a ReShade add-on, by any of <see cref="AnyAddonExtensions"/>.</summary>
    public static bool IsAddon(string name) => EndsWithAny(name, AnyAddonExtensions);

    /// <summary>A set of file names, case ignored.</summary>
    public static FrozenSet<string> Set(params IEnumerable<string> names) => names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static bool EndsWithAny(string name, IReadOnlyList<string> extensions) =>
        extensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
}
