using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library.Emulators;

/// <summary>What an emulator that cannot present through Direct3D really draws with.</summary>
internal static class EmulatorApi
{
    /// <summary>
    /// The API in use when nobody chose one: what the executable said, unless it is an emulator that
    /// cannot present through Direct3D and its imports read as Direct3D. eden.exe is a Qt program whose
    /// imports read as DX12, and the Direct3D routes were offered for it. The scan, "back to
    /// automatic" and the label beside the dropdown all ask this.
    /// </summary>
    public static (GraphicsApi Api, string Explanation) Automatic(ApiDecision detected, EmulatorProfile? emulator) =>
        emulator is { Fallback: { } fallback } && IsDirect3D(detected.Api)
            ? (fallback, Texts.Format("EmulatorApiFallback", emulator.Name, ApiNames.Of(detected.Api)))
            : (detected.Api, detected.Explanation);

    private static bool IsDirect3D(GraphicsApi api) => api is GraphicsApi.DX8 or GraphicsApi.DX9 or GraphicsApi.DX10 or GraphicsApi.DX11 or GraphicsApi.DX12 or GraphicsApi.Dxgi;
}
