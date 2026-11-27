namespace Autopilot.Core.Routes;

/// <summary>The FSR 2/3 or XeSS runtime a game without DLSS of its own ships, for OptiScaler to hook.</summary>
public enum Upscaler
{
    /// <summary>No FSR 2/3 or XeSS runtime, or the game ships DLSS of its own and the question was not asked.</summary>
    None,

    /// <summary>AMD FSR 2/3; a game shipping both FSR and XeSS counts as FSR, OptiScaler's most exercised input.</summary>
    Fsr,

    /// <summary>Intel XeSS.</summary>
    Xess,
}

/// <summary>
/// What a game ships of its own, read from the names of the files in its folders: the facts the
/// route decision takes as inputs. Files this tool wrote are never counted. Evidence paths are
/// relative to the game folder.
/// </summary>
/// <param name="OwnDlss">
/// Whether the game ships super-resolution DLSS: a Streamline file, a DLSS runtime renamed to switch
/// it off, or an <c>nvngx_dlss.dll</c> that is not this tool's.
/// </param>
/// <param name="DlssEvidence">
/// The DLSS files found, sorted; <c>nvngx_dlssg.dll</c> and <c>nvngx_dlssd.dll</c> are listed too,
/// though neither alone makes <paramref name="OwnDlss"/> true.
/// </param>
/// <param name="Upscaler">The FSR 2/3 or XeSS runtime shipped; <see cref="Upscaler.None"/> whenever <paramref name="OwnDlss"/> is true.</param>
/// <param name="UpscalerEvidence">The FSR 2/3 and XeSS files that decided <paramref name="Upscaler"/>, sorted.</param>
/// <param name="ShipsRayReconstruction">The game ships <c>nvngx_dlssd.dll</c>, which the ray-reconstruction setting needs.</param>
/// <param name="ShipsFrameGeneration">
/// The game ships frame generation of its own (<c>nvngx_dlssg.dll</c> or <c>sl.dlss_g.dll</c>),
/// which the RTX 40 multi-frame option needs.
/// </param>
/// <param name="RemixRuntime">The full path of the RTX Remix <c>.trex</c> folder holding <c>d3d9.dll</c>, or null.</param>
public sealed record GameRuntimes(
    bool OwnDlss,
    IReadOnlyList<string> DlssEvidence,
    Upscaler Upscaler,
    IReadOnlyList<string> UpscalerEvidence,
    bool ShipsRayReconstruction,
    bool ShipsFrameGeneration,
    string? RemixRuntime);
