using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Tests.Install;

/// <summary>Route settings as a full record keeps them.</summary>
internal static class FullChoices
{
    /// <summary>A setting away from its default in every part the record keeps, the person's own add-on and an exact feeder tag included.</summary>
    public static RouteSettings Value => new()
    {
        ReShadeLoadsAs = ReShadeProxy.D3d12,
        OptiScalerLoadsAs = OptiScalerProxy.Winmm,
        OptiScalerBuild = OptiScalerBuildKey.Wilsjo2Mfg,
        OptiScalerPreset = NeuralPreset.Preset2,
        OptiScalerStyle = NeuralStyle.Cinematic,
        OptiScalerPasses = NeuralPasses.Three,
        ModelResolution = 75,
        FrameGeneration = true,
        OwnFrameGeneration = OwnFrameGeneration.Sm86Dbghelp,
        MotionProvider = MotionProvider.BuiltIn,
        FeederPreset = DlssPreset.K,
        FeederHdr = FeederHdr.Hdr,
        FeederBuild = FeederChoice.Exact("v2.0.0"),
        HelperProcess = true,
        Dxvk = false,
        RenodxLabel = "4.55",
        RenodxFile = @"C:\Mods\renodx-dlss5.addon64",
        DlssnrLabel = "310.4.0",
        DlssLabel = "310.3.0",
        KeepGameDlss = false,
        RayReconstructionLabel = "310.2.1",
        MultiFrameGeneration = true,
        OverlayKey = 0x24,
        VrHeadset = true,
        SwapRemixRuntime = true,
        UseNvidiaCard = false,
    };
}
