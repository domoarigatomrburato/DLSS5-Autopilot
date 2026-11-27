using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;
using static Autopilot.Core.Tests.Routes.RouteGames;

namespace Autopilot.Core.Tests.Routes;

/// <summary>The note beside each route in the dropdown, whether it is usable, and its reliability level per API and architecture.</summary>
[TestClass]
public sealed class RouteFitTests
{
    [TestMethod]
    [DataRow(RouteId.Native, GraphicsApi.DX12, true, "RouteFitNative")]
    [DataRow(RouteId.Remix, GraphicsApi.DX9, false, "RouteFitRemix")]
    [DataRow(RouteId.Standalone, GraphicsApi.DX11, false, "RouteFitStandalone")]
    [DataRow(RouteId.Uplift, GraphicsApi.OpenGL, false, "RouteFitUplift")]
    [DataRow(RouteId.Renodx, GraphicsApi.DX12, false, "RouteFitRenodx")]
    [DataRow(RouteId.Upstream, GraphicsApi.DX12, true, "RouteFitUpstream")]
    [DataRow(RouteId.Bridge, GraphicsApi.DX11, true, "RouteFitBridgeTested")]
    [DataRow(RouteId.Bridge, GraphicsApi.Vulkan, false, "RouteFitBridgeTested")]
    [DataRow(RouteId.Bridge, GraphicsApi.DX12, false, "RouteFitBridge")]
    [DataRow(RouteId.Bridge, GraphicsApi.Dxgi, true, "RouteFitBridge")]
    [DataRow(RouteId.Feeder, GraphicsApi.DX12, true, "RouteFitFeederOwnDlss")]
    [DataRow(RouteId.Feeder, GraphicsApi.Vulkan, true, "RouteFitFeederOwnDlss")]
    [DataRow(RouteId.Feeder, GraphicsApi.DX11, false, "RouteFitFeeder")]
    [DataRow(RouteId.OptiScaler, GraphicsApi.DX12, true, "RouteFitOptiScaler")]
    [DataRow(RouteId.OptiScaler, GraphicsApi.DX11, true, "RouteFitOptiScalerDx11")]
    [DataRow(RouteId.OptiScaler, GraphicsApi.Vulkan, true, "RouteFitOptiScalerVulkan")]
    public void Each_usable_route_carries_its_note(RouteId route, GraphicsApi api, bool ownDlss, string notes)
    {
        Fit fit = RouteFit.Of(route, api, Runtimes(ownDlss), 120);

        Assert.IsTrue(fit.Usable);
        Assert.AreEqual(notes, fit.Notes.Keys());
        Assert.IsNotEmpty(fit.Notes[0].Text());
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX11, Upscaler.Fsr, "RouteFitOptiScalerDx11,RouteFitOptiScalerUpscalerOn,RouteFitOptiScaler", "FSR")]
    [DataRow(GraphicsApi.DX12, Upscaler.Xess, "RouteFitOptiScalerUpscalerOn,RouteFitOptiScaler", "XeSS")]
    public void Optiscaler_without_dlss_says_the_upscaler_has_to_be_on(GraphicsApi api, Upscaler upscaler, string notes, string kind)
    {
        Fit fit = RouteFit.Of(RouteId.OptiScaler, api, Runtimes(upscaler: upscaler), 120);

        Assert.AreEqual(notes, fit.Notes.Keys());
        RouteMessage on = fit.Notes.Single(note => note.Key == "RouteFitOptiScalerUpscalerOn");
        Assert.AreEqual(kind, on.Joined());
        Assert.Contains(kind, on.Text());
    }

    [TestMethod]
    public void Optiscaler_on_vulkan_below_rtx_50_adds_the_card_note() =>
        Assert.AreEqual("RouteFitOptiScalerVulkan,RouteFitOptiScalerRtx50Only", RouteFit.Of(RouteId.OptiScaler, GraphicsApi.Vulkan, Runtimes(ownDlss: true), 86).Notes.Keys());

    [TestMethod]
    public void Optiscaler_with_nothing_to_hook_is_not_usable()
    {
        Fit fit = RouteFit.Of(RouteId.OptiScaler, GraphicsApi.DX12, Runtimes(), 120);

        Assert.IsFalse(fit.Usable);
        Assert.AreEqual("RouteFitOptiScalerNeedsUpscaler", fit.Notes.Keys());
    }

    [TestMethod]
    public void Upstream_without_dlss_is_not_usable()
    {
        Fit fit = RouteFit.Of(RouteId.Upstream, GraphicsApi.DX12, Runtimes(upscaler: Upscaler.Fsr), 120);

        Assert.IsFalse(fit.Usable);
        Assert.AreEqual("RouteFitUpstreamNeedsDlss", fit.Notes.Keys());
    }

    [TestMethod]
    [DataRow(RouteId.Native, GraphicsApi.DX12, 64, RouteReliability.Stable)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX11, 64, RouteReliability.Stable)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX12, 64, RouteReliability.Stable)]
    [DataRow(RouteId.Feeder, GraphicsApi.Dxgi, 64, RouteReliability.Stable)]
    [DataRow(RouteId.Upstream, GraphicsApi.DX12, 64, RouteReliability.Beta)]
    [DataRow(RouteId.Bridge, GraphicsApi.DX11, 64, RouteReliability.Beta)]
    [DataRow(RouteId.Bridge, GraphicsApi.Vulkan, 64, RouteReliability.Beta)]
    [DataRow(RouteId.OptiScaler, GraphicsApi.DX12, 64, RouteReliability.Beta)]
    [DataRow(RouteId.Remix, GraphicsApi.DX9, 32, RouteReliability.Beta)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX10, 64, RouteReliability.Beta)]
    [DataRow(RouteId.Feeder, GraphicsApi.Vulkan, 64, RouteReliability.Beta)]
    [DataRow(RouteId.Feeder, GraphicsApi.OpenGL, 64, RouteReliability.Beta)]
    [DataRow(RouteId.Renodx, GraphicsApi.DX9, 64, RouteReliability.Beta)]
    [DataRow(RouteId.Standalone, GraphicsApi.DX12, 64, RouteReliability.Experimental)]
    [DataRow(RouteId.Uplift, GraphicsApi.DX12, 64, RouteReliability.Experimental)]
    [DataRow(RouteId.Renodx, GraphicsApi.DX11, 64, RouteReliability.Experimental)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX9, 32, RouteReliability.Experimental)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX8, 32, RouteReliability.Experimental)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX11, 32, RouteReliability.Experimental)]
    [DataRow(RouteId.Feeder, GraphicsApi.DX10, 32, RouteReliability.Experimental)]
    [DataRow(RouteId.Feeder, GraphicsApi.Vulkan, 32, RouteReliability.Experimental)]
    public void Each_route_has_its_reliability_level(RouteId route, GraphicsApi api, int bits, RouteReliability level) =>
        Assert.AreEqual(level, RouteFit.Level(route, api, (Bitness)Bits(bits)!));

    [TestMethod]
    public void The_decision_gives_each_option_its_level()
    {
        RouteChoice choice = Decide(GraphicsApi.DX12, ownDlss: true);

        CollectionAssert.AreEqual(
            new[]
            {
                RouteReliability.Stable, RouteReliability.Beta, RouteReliability.Beta, RouteReliability.Beta,
                RouteReliability.Stable, RouteReliability.Experimental, RouteReliability.Experimental, RouteReliability.Experimental,
            },
            choice.Options.Select(option => option.Reliability).ToArray());
    }
}
