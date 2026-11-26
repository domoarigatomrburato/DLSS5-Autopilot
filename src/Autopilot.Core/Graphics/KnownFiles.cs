using System.Collections.Frozen;

namespace Autopilot.Core.Graphics;

/// <summary>
/// The file names the tool knows by heart, in one place: its own add-ons, other DLSS tools' files,
/// the NGX runtimes and the DLLs whose contents say nothing about a game. Every set ignores case.
/// </summary>
internal static class KnownFiles
{
    public const string InstallRecord = "dlss5-autopilot.json";

    public const string BackupSuffix = ".dlss5-autopilot-backup";

    public const string AgilityFolder = "D3D12";

    public const string AgilityCore = "D3D12Core.dll";

    public const string Dlss = "nvngx_dlss.dll";

    public const string FrameGeneration = "nvngx_dlssg.dll";

    public const string RayReconstruction = "nvngx_dlssd.dll";

    public const string UnityPlayer = "UnityPlayer.dll";

    /// <summary>The game's own NGX runtimes, in the order they are named as evidence.</summary>
    public static readonly IReadOnlyList<string> NgxRuntimes = [Dlss, FrameGeneration, RayReconstruction];

    /// <summary>The NGX runtimes that run on Direct3D 12 only.</summary>
    public static readonly IReadOnlyList<string> D3d12OnlyRuntimes = [FrameGeneration, RayReconstruction];

    /// <summary>Renderer DLL names a game loads, ranked: a Direct3D name outranks OpenGL and Vulkan.</summary>
    public static readonly IReadOnlyList<(string Name, GraphicsApi Api)> Renderers =
    [
        ("d3d12.dll", GraphicsApi.DX12),
        ("d3d11.dll", GraphicsApi.DX11),
        ("d3d10.dll", GraphicsApi.DX10),
        ("dxgi.dll", GraphicsApi.Dxgi),
        ("vulkan-1.dll", GraphicsApi.Vulkan),
        ("opengl32.dll", GraphicsApi.OpenGL),
        ("d3d9.dll", GraphicsApi.DX9),
        ("d3d8.dll", GraphicsApi.DX8),
    ];

    /// <summary>Names engines that can drive several backends carry without drawing with them.</summary>
    public static readonly FrozenSet<string> Ambiguous = Set("opengl32.dll", "vulkan-1.dll");

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
        "dlssg_sm86.ini",
        "nvngx_dlssnr.ini",
        "gitc-uplift-helper64.exe",
        "uplift-helper64.exe");

    /// <summary>Words that make a ReShade add-on another DLSS tool's ("dlss" covers dlssg and dlssd, "ngx" nvngx).</summary>
    public static readonly IReadOnlyList<string> NgxAddonWords = ["dlss", "ngx", "swapper"];

    /// <summary>ReShade add-on extensions.</summary>
    public static readonly IReadOnlyList<string> AddonExtensions = [".addon64", ".addon32"];

    /// <summary>
    /// DLLs beside a game whose imports and strings say nothing about it: proxies (DXVK, ReShade,
    /// wrappers) and the DLSS, OptiScaler, Streamline, XeSS and FidelityFX files routes and games ship.
    /// </summary>
    public static readonly FrozenSet<string> NotTheGame = Set(
    [
        .. Renderers.Select(renderer => renderer.Name),
        "reshade32.dll", "reshade64.dll", "reshade.dll", "ddraw.dll", "dinput8.dll", "winmm.dll",
        "version.dll", "dbghelp.dll", "winhttp.dll", "wininet.dll", "d3dcompiler_47.dll",
        "nvngx_dlssnr.dll", Dlss, RayReconstruction, FrameGeneration, "_nvngx.dll", "nvngx-wrapper.dll",
        "remix_nvngx.dll", "optiscaler.dll", "dlss5-bridge.dll", "dlss-enabler.dll",
        "dlss-enabler-headless.dll", "rtx40mfgcore.dll", "sl.interposer.dll", "sl.common.dll",
        "sl.reflex.dll", "sl.dlss.dll", "sl.dlss_g.dll", "sl.dlss_d.dll", "sl.pcl.dll", "libxess.dll",
        "libxess_dx11.dll", "libxess_fg.dll", "libxell.dll", "amd_fidelityfx_dx12.dll",
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

    /// <summary>Whether a file in a game's folder belongs to another DLSS or NGX tool.</summary>
    public static bool IsForeignNgx(string name) =>
        ForeignNgxFiles.Contains(name)
        || (AddonExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            && !OurAddons.Contains(name)
            && NgxAddonWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));

    private static FrozenSet<string> Set(params IEnumerable<string> names) => names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
