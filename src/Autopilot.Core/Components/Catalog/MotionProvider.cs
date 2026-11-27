namespace Autopilot.Core.Components.Catalog;

/// <summary>
/// Where the feeder reads its motion vectors from: the number written as <c>DLSS5_MV_PROVIDER</c>.
/// 4 (LumeniteFX QuantMotion) is not offered: LumeniteFX renamed it PocketMV and the feeder does not
/// read it yet (<see cref="Routes.MotionProviderSetting"/> refuses a setting that names it).
/// </summary>
public enum MotionProvider
{
    /// <summary>A generic <c>texMotionVectors</c> shader the person installs.</summary>
    Generic = 0,

    /// <summary>iMMERSE Launchpad, which the person installs.</summary>
    Launchpad = 1,

    /// <summary>VORT's <c>vort_Motion.fx</c>.</summary>
    Vort = 2,

    /// <summary>LumeniteFX's <c>lumenite_Kernel.fx</c>, the default.</summary>
    LumeniteKernel = 3,

    /// <summary>The feeder's own provider (feeder 2.0.0 or newer).</summary>
    BuiltIn = 5,
}
