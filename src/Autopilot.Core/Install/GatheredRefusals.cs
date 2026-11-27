using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>
/// The refusals that need a version only known once the components are gathered, as functions of that version:
/// still before the first write, since gathering writes only to the download cache.
/// </summary>
internal static class GatheredRefusals
{
    /// <summary>The first ReShade GITC-Uplift runs on under Vulkan and OpenGL.</summary>
    private static readonly ComponentVersion _upliftReShade = new("6.8.0");

    /// <summary>
    /// What the feeder build <paramref name="feeder"/> refuses for a game on <paramref name="api"/> with <paramref name="read"/>:
    /// Direct3D 10, the helper process and the built-in provider each need a first build. Wired once the build is gathered.
    /// </summary>
    public static IReadOnlyList<InstallBlocker> FeederBuild(ComponentVersion feeder, GraphicsApi api, RouteSettings read)
    {
        List<FeederNeed> needs = [];
        if (api == GraphicsApi.DX10)
        {
            needs.Add(FeederNeed.Direct3D10);
        }

        if (read.HelperProcess)
        {
            needs.Add(FeederNeed.HelperProcess);
        }

        if (read.MotionProvider == MotionProvider.BuiltIn)
        {
            needs.Add(FeederNeed.BuiltInProvider);
        }

        return [.. FeederPins.Check(feeder, needs).Refusals.Select(text => new InstallBlocker(BlockerKind.FeederBuild, text))];
    }

    /// <summary>Why GITC-Uplift cannot go onto a game on <paramref name="api"/> with the ReShade the PC can get, <paramref name="reShade"/>; null when it can.</summary>
    public static InstallBlocker? UpliftReShade(ComponentVersion reShade, GraphicsApi api) =>
        api is GraphicsApi.Vulkan or GraphicsApi.OpenGL && reShade.CompareTo(_upliftReShade) == VersionOrder.Older
            ? new(BlockerKind.UpliftReShade, Texts.Format("RefusalUpliftReShade", reShade.Label, _upliftReShade.Label))
            : null;
}
