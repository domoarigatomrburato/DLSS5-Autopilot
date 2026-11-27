using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;
using static Autopilot.Core.Tests.Routes.RouteGames;

namespace Autopilot.Core.Tests.Routes;

/// <summary>The card moves the recommendation to OptiScaler on RTX 50 only, and the driver warns per route without moving anything.</summary>
[TestClass]
public sealed class RouteSteeringTests
{
    [TestMethod]
    [DataRow(75, "RouteOptiScalerOlderCard")]
    [DataRow(86, "RouteOptiScalerOlderCard")]
    [DataRow(89, "RouteOptiScalerOlderCard")]
    [DataRow(null, "RouteOptiScalerCardUnknown")]
    public void Own_dlss_on_d3d12_below_rtx_50_or_on_an_unknown_card_keeps_native(int? sm, string cardNote)
    {
        RouteChoice choice = Decide(GraphicsApi.DX12, ownDlss: true, gpu: Card(sm, "616.56"));

        Assert.AreEqual(RouteId.Native, choice.Recommended);
        Assert.AreEqual("RouteDx12OwnDlss", choice.Reason.Key);
        RouteOption optiScaler = choice.Option(RouteId.OptiScaler);
        Assert.IsTrue(optiScaler.Usable);
        Assert.AreEqual(sm is null ? "RouteFitOptiScaler" : "RouteFitOptiScaler,RouteFitOptiScalerRtx50Only", optiScaler.Notes.Keys());
        Assert.AreEqual(cardNote, optiScaler.Warnings.Keys());
    }

    [TestMethod]
    public void Own_dlss_on_d3d12_with_no_card_read_keeps_native()
    {
        var noCard = GpuFacts.From(new GpuReading([], null, []));

        RouteChoice choice = Decide(GraphicsApi.DX12, ownDlss: true, gpu: noCard);

        Assert.AreEqual(RouteId.Native, choice.Recommended);
        Assert.AreEqual("RouteOptiScalerCardUnknown", choice.Option(RouteId.OptiScaler).Warnings.Keys());
    }

    [TestMethod]
    public void Own_dlss_on_d3d12_and_no_facts_at_all_keeps_native() =>
        Assert.AreEqual(RouteId.Native, Decide(GraphicsApi.DX12, ownDlss: true, gpu: null).Recommended);

    [TestMethod]
    public void Own_dlss_on_d3d12_on_rtx_50_recommends_optiscaler_with_native_still_offered()
    {
        RouteChoice choice = Decide(GraphicsApi.DX12, ownDlss: true, gpu: Card(120, "616.56"));

        Assert.AreEqual(RouteId.OptiScaler, choice.Recommended);
        Assert.AreEqual("RouteOptiScalerRtx50", choice.Reason.Key);
        Assert.IsTrue(choice.Allows(RouteId.Native));
        Assert.AreEqual("RouteFitOptiScaler", choice.RecommendedOption!.Notes.Keys());
        Assert.IsEmpty(choice.RecommendedOption.Warnings);
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX11, "bridge")]
    [DataRow(GraphicsApi.Dxgi, null)]
    public void Rtx_50_steers_only_d3d12(GraphicsApi api, string? recommended) =>
        Assert.AreEqual(recommended, Decide(api, ownDlss: true, gpu: Card(120, "616.56")).RecommendedId());

    [TestMethod]
    [DataRow(89, Upscaler.Fsr, "feeder", "RouteUpscaler")]
    [DataRow(null, Upscaler.Fsr, "feeder", "RouteUpscaler")]
    [DataRow(120, Upscaler.Fsr, "optiscaler", "RouteOptiScalerUpscalerRtx50")]
    [DataRow(120, Upscaler.Xess, "optiscaler", "RouteOptiScalerUpscalerRtx50")]
    public void An_fsr_or_xess_game_on_d3d12_goes_to_optiscaler_on_rtx_50_only(int? sm, Upscaler upscaler, string recommended, string reason)
    {
        RouteChoice choice = Decide(GraphicsApi.DX12, upscaler: upscaler, gpu: Card(sm, "616.56"));

        Assert.AreEqual(recommended, choice.RecommendedId());
        Assert.AreEqual(reason, choice.Reason.Key);
        Assert.IsNotEmpty(choice.Reason.Text());
        Assert.IsTrue(choice.Allows(RouteId.Feeder));
        Assert.AreEqual(
            sm is 89 ? "RouteFitOptiScalerUpscalerOn,RouteFitOptiScaler,RouteFitOptiScalerRtx50Only" : "RouteFitOptiScalerUpscalerOn,RouteFitOptiScaler",
            choice.Option(RouteId.OptiScaler).Notes.Keys());
    }

    [TestMethod]
    public void An_fsr_game_named_by_dxgi_keeps_the_feeder_on_rtx_50() =>
        Assert.AreEqual(RouteId.Feeder, Decide(GraphicsApi.Dxgi, upscaler: Upscaler.Fsr, gpu: Card(120, "616.56")).Recommended);

    // Driver warnings on a D3D12 game with own DLSS, where every route but remix is offered (standalone included).
    [TestMethod]
    [DataRow("616.55", RouteId.Native, "RouteDriverTooOld")]
    [DataRow("616.55", RouteId.OptiScaler, "RouteDriverTooOld,RouteOptiScalerOlderCard")]
    [DataRow("616.55", RouteId.Standalone, "RouteDriverTooOld")]
    [DataRow("616.55", RouteId.Uplift, "RouteDriverTooOld")]
    [DataRow("616.56", RouteId.Native, "")]
    [DataRow("616.56", RouteId.Renodx, "")]
    [DataRow("616.56", RouteId.OptiScaler, "RouteOptiScalerOlderCard")]
    [DataRow("616.64", RouteId.Native, "RouteDriverRenodxFault,RouteDriverTryStandalone")]
    [DataRow("616.64", RouteId.Bridge, "RouteDriverRenodxFault,RouteDriverTryStandalone")]
    [DataRow("616.64", RouteId.Feeder, "RouteDriverRenodxFault,RouteDriverTryStandalone")]
    [DataRow("616.64", RouteId.Renodx, "RouteDriverShortFuse,RouteDriverTryStandalone")]
    [DataRow("616.64", RouteId.Standalone, "RouteDriverStandalone")]
    [DataRow("616.64", RouteId.Upstream, "RouteDriverGeneric")]
    [DataRow("616.64", RouteId.Uplift, "RouteDriverGeneric")]
    [DataRow("616.92", RouteId.Native, "RouteDriverRenodxFault,RouteDriverTryStandalone")]
    [DataRow("616.92", RouteId.OptiScaler, "RouteDriverGeneric,RouteOptiScalerOlderCard")]
    [DataRow("616.92", RouteId.Standalone, "RouteDriverStandalone")]
    [DataRow(null, RouteId.Native, "")]
    [DataRow(null, RouteId.Upstream, "")]
    [DataRow(null, RouteId.OptiScaler, "RouteOptiScalerOlderCard")]
    [DataRow("not a version", RouteId.Native, "")]
    public void The_driver_warns_per_route(string? driver, RouteId route, string warnings)
    {
        RouteChoice choice = Decide(GraphicsApi.DX12, ownDlss: true, gpu: Card(89, driver));

        RouteOption option = choice.Option(route);
        Assert.AreEqual(warnings, option.Warnings.Keys());
        foreach (RouteMessage warning in option.Warnings)
        {
            Assert.IsNotEmpty(warning.Text());
        }
    }

    [TestMethod]
    [DataRow("616.55")]
    [DataRow("616.64")]
    [DataRow("616.92")]
    [DataRow(null)]
    public void The_driver_never_moves_the_recommendation(string? driver)
    {
        Assert.AreEqual(RouteId.Native, Decide(GraphicsApi.DX12, ownDlss: true, gpu: Card(89, driver)).Recommended);
        Assert.AreEqual(RouteId.Feeder, Decide(GraphicsApi.DX11, gpu: Card(89, driver)).Recommended);
        Assert.AreEqual(RouteId.Feeder, Decide(GraphicsApi.Vulkan, gpu: Card(89, driver)).Recommended);
    }

    [TestMethod]
    public void On_a_faulting_driver_the_recommended_feeder_names_the_driver_and_standalone()
    {
        RouteChoice choice = Decide(GraphicsApi.DX11, gpu: Card(89, "616.92"));

        IReadOnlyList<RouteMessage> warnings = choice.RecommendedOption!.Warnings;
        Assert.AreEqual("RouteDriverRenodxFault,RouteDriverTryStandalone", warnings.Keys());
        Assert.AreEqual("616.92|616.64|4.55|616.56", warnings[0].Joined());
        Assert.Contains("616.92", warnings[0].Text());
        Assert.Contains("handful of reports", warnings[0].Text());
    }

    [TestMethod]
    public void Standalone_is_not_named_where_it_is_not_offered()
    {
        RouteChoice choice = Decide(GraphicsApi.Vulkan, gpu: Card(89, "616.92"));

        Assert.AreEqual("RouteDriverRenodxFault", choice.RecommendedOption!.Warnings.Keys());
        Assert.AreEqual("RouteDriverShortFuse", Decide(GraphicsApi.DX9, gpu: Card(89, "616.92")).RecommendedOption!.Warnings.Keys());
    }

    [TestMethod]
    [DataRow("616.55", "RouteDriverTooOld,RouteOptiScalerDriver")]
    [DataRow("616.56", "")]
    [DataRow(null, "")]
    public void Optiscaler_on_rtx_50_says_when_the_driver_is_too_old_for_it(string? driver, string warnings)
    {
        RouteOption optiScaler = Decide(GraphicsApi.DX12, ownDlss: true, gpu: Card(120, driver)).Option(RouteId.OptiScaler);

        Assert.AreEqual(warnings, optiScaler.Warnings.Keys());
        if (driver is not null && warnings.Length > 0)
        {
            Assert.AreEqual($"616.56|{driver}", optiScaler.Warnings[^1].Joined());
        }
    }
}
