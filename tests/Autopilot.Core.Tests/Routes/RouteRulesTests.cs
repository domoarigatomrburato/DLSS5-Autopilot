using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;
using static Autopilot.Core.Tests.Routes.RouteGames;

namespace Autopilot.Core.Tests.Routes;

/// <summary>Which routes a game is offered, in dropdown order, which is recommended and why: the rules table, uplift and the Remix override.</summary>
[TestClass]
public sealed class RouteRulesTests
{
    // The expectation table: the game (API, bitness, own DLSS, upscaler), then the options, the
    // recommended route ("" for none) and the reason's key. No card is known, so nothing steers.
    [TestMethod]
    [DataRow(GraphicsApi.DX10, 64, false, Upscaler.None, "feeder,uplift", "feeder", "RouteDx10", DisplayName = "1 D3D10 64-bit")]
    [DataRow(GraphicsApi.DX10, 32, true, Upscaler.None, "feeder,uplift", "feeder", "RouteDx10", DisplayName = "1 D3D10 32-bit with own DLSS")]
    [DataRow(GraphicsApi.DX11, 32, true, Upscaler.None, "feeder,uplift", "feeder", "Route32Bit", DisplayName = "2 32-bit D3D11 with own DLSS")]
    [DataRow(GraphicsApi.DX12, 32, false, Upscaler.Fsr, "feeder,uplift", "feeder", "Route32Bit", DisplayName = "2 32-bit D3D12 with FSR")]
    [DataRow(GraphicsApi.DX9, 32, false, Upscaler.None, "feeder,uplift", "feeder", "Route32Bit", DisplayName = "2 32-bit D3D9")]
    [DataRow(GraphicsApi.DX8, 32, false, Upscaler.None, "feeder", "feeder", "Route32Bit", DisplayName = "2 32-bit D3D8, no uplift")]
    [DataRow(GraphicsApi.Vulkan, 32, true, Upscaler.None, "feeder,uplift", "feeder", "Route32Bit", DisplayName = "2 32-bit Vulkan")]
    [DataRow(GraphicsApi.OpenGL, 32, false, Upscaler.None, "feeder,uplift", "feeder", "Route32Bit", DisplayName = "2 32-bit OpenGL")]
    [DataRow(GraphicsApi.Unknown, 32, false, Upscaler.None, "feeder,uplift", "feeder", "Route32Bit", DisplayName = "2 32-bit unknown API")]
    [DataRow(GraphicsApi.DX9, 64, false, Upscaler.None, "renodx,uplift", "renodx", "RouteDx9On64", DisplayName = "3 64-bit D3D9")]
    [DataRow(GraphicsApi.DX9, 64, true, Upscaler.None, "renodx,uplift", "renodx", "RouteDx9On64", DisplayName = "3 64-bit D3D9 with own DLSS")]
    [DataRow(GraphicsApi.Vulkan, 64, true, Upscaler.None, "bridge,feeder,uplift", "bridge", "RouteVulkanOwnDlss", DisplayName = "4 Vulkan with own DLSS")]
    [DataRow(GraphicsApi.Vulkan, 64, false, Upscaler.None, "feeder,bridge,uplift", "feeder", "RouteVulkan", DisplayName = "5 Vulkan without")]
    [DataRow(GraphicsApi.Vulkan, 64, false, Upscaler.Fsr, "feeder,bridge,uplift", "feeder", "RouteVulkan", DisplayName = "5 Vulkan with FSR")]
    [DataRow(GraphicsApi.OpenGL, 64, false, Upscaler.None, "feeder,uplift", "feeder", "RouteOpenGl", DisplayName = "6 OpenGL")]
    [DataRow(GraphicsApi.OpenGL, 64, true, Upscaler.None, "feeder,uplift", "feeder", "RouteOpenGl", DisplayName = "6 OpenGL with own DLSS")]
    [DataRow(GraphicsApi.DX11, 64, true, Upscaler.None, "bridge,optiscaler,feeder,standalone,renodx,uplift", "bridge", "RouteDx11OwnDlss", DisplayName = "7 D3D11 with own DLSS")]
    [DataRow(GraphicsApi.DX12, 64, true, Upscaler.None, "native,upstream,optiscaler,bridge,feeder,standalone,renodx,uplift", "native", "RouteDx12OwnDlss", DisplayName = "8 D3D12 with own DLSS")]
    [DataRow(GraphicsApi.Dxgi, 64, true, Upscaler.None, "optiscaler,bridge,feeder,standalone,renodx,uplift", "", "RouteAskApi", DisplayName = "8a DXGI with own DLSS")]
    [DataRow(GraphicsApi.Unknown, 64, true, Upscaler.None, "optiscaler,bridge,feeder,standalone,renodx,uplift", "", "RouteAskApi", DisplayName = "8a unknown API with own DLSS")]
    [DataRow(GraphicsApi.DX11, 64, false, Upscaler.Fsr, "feeder,optiscaler,bridge,standalone,renodx,uplift", "feeder", "RouteUpscaler", DisplayName = "9 D3D11 with FSR")]
    [DataRow(GraphicsApi.DX12, 64, false, Upscaler.Xess, "feeder,optiscaler,bridge,standalone,renodx,uplift", "feeder", "RouteUpscaler", DisplayName = "9 D3D12 with XeSS")]
    [DataRow(GraphicsApi.Dxgi, 64, false, Upscaler.Fsr, "feeder,optiscaler,bridge,standalone,renodx,uplift", "feeder", "RouteUpscaler", DisplayName = "9 DXGI with FSR")]
    [DataRow(GraphicsApi.DX11, 64, false, Upscaler.None, "feeder,bridge,standalone,renodx,uplift", "feeder", "RouteNoDlss", DisplayName = "10 D3D11, nothing")]
    [DataRow(GraphicsApi.DX12, 64, false, Upscaler.None, "feeder,bridge,standalone,renodx,uplift", "feeder", "RouteNoDlss", DisplayName = "10 D3D12, nothing")]
    [DataRow(GraphicsApi.Dxgi, 64, false, Upscaler.None, "feeder,bridge,standalone,renodx,uplift", "feeder", "RouteNoDlss", DisplayName = "10 DXGI, nothing")]
    [DataRow(GraphicsApi.Unknown, 64, false, Upscaler.Fsr, "feeder,optiscaler,bridge,standalone,renodx,uplift", "", "RouteAskApi", DisplayName = "unknown API with FSR: asked")]
    [DataRow(GraphicsApi.Unknown, 64, false, Upscaler.None, "feeder,bridge,standalone,renodx,uplift", "", "RouteAskApi", DisplayName = "unknown API, nothing: asked")]
    public void Each_game_gets_the_rows_options_recommendation_and_reason(GraphicsApi api, int bits, bool ownDlss, Upscaler upscaler, string options, string recommended, string reason)
    {
        RouteChoice choice = Decide(api, bits, ownDlss, upscaler);

        Assert.AreEqual(options, choice.Ids());
        Assert.AreEqual(recommended, choice.RecommendedId() ?? "");
        Assert.AreEqual(reason, choice.Reason.Key);
        Assert.IsNull(choice.MovedBy);
        foreach (RouteMessage message in choice.Messages())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(message.Text()), message.Key);
        }
    }

    [TestMethod]
    public void A_shipped_upscaler_is_named_with_its_files_in_the_reason()
    {
        RouteChoice choice = Decide(GraphicsApi.DX12, upscaler: Upscaler.Xess);

        Assert.AreEqual("XeSS|ffx_fsr2_api_x64.dll", choice.Reason.Joined());
        Assert.Contains("XeSS", choice.Reason.Text());
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX9, "RouteNoteDxvkDx9")]
    [DataRow(GraphicsApi.DX8, "RouteNoteDxvkDx8")]
    [DataRow(GraphicsApi.DX11, "")]
    public void A_32_bit_d3d8_or_d3d9_game_is_told_dxvk_comes_first(GraphicsApi api, string note) =>
        Assert.AreEqual(note, Decide(api, 32).Notes.Keys());

    [TestMethod]
    public void When_the_api_is_not_settled_the_person_is_asked_and_shown_what_the_executable_said()
    {
        ApiDecision detected = new(GraphicsApi.Dxgi, ApiReason.StaticImport, "dxgi.dll in the import table");

        RouteChoice choice = Decide(GraphicsApi.Dxgi, ownDlss: true, detected: detected);

        Assert.IsNull(choice.Recommended);
        Assert.IsNull(choice.RecommendedOption);
        Assert.AreEqual(RouteQuestion.Api, choice.Asks);
        Assert.AreSame(detected, choice.ApiEvidence);
        Assert.AreEqual(ApiConfidence.Read, choice.ApiEvidence!.Confidence);
    }

    [TestMethod]
    public void A_settled_api_asks_nothing_and_carries_no_evidence()
    {
        RouteChoice choice = Decide(GraphicsApi.DX12, ownDlss: true, detected: new ApiDecision(GraphicsApi.DX12, ApiReason.StaticImport, "d3d12.dll"));

        Assert.AreEqual(RouteQuestion.None, choice.Asks);
        Assert.IsNull(choice.ApiEvidence);
        Assert.AreEqual(RouteId.Native, choice.RecommendedOption!.Route);
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX12, null)]
    [DataRow(GraphicsApi.DX9, "C:/Games/Portal/.trex")]
    public void An_unknown_architecture_offers_nothing_and_asks_for_it(GraphicsApi api, string? remix)
    {
        RouteChoice choice = Decide(api, bits: 0, ownDlss: true, remix: remix);

        Assert.IsEmpty(choice.Options);
        Assert.IsNull(choice.Recommended);
        Assert.AreEqual(RouteQuestion.Architecture, choice.Asks);
        Assert.AreEqual("RouteChooseArchitecture", choice.Reason.Key);
        Assert.IsFalse(choice.Allows(RouteId.Feeder));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("C:/Games/Old/.trex")]
    public void D3d8_on_64_bit_offers_nothing(string? remix)
    {
        RouteChoice choice = Decide(GraphicsApi.DX8, 64, remix: remix);

        Assert.IsEmpty(choice.Options);
        Assert.IsNull(choice.Recommended);
        Assert.AreEqual("RouteDx8On64", choice.Reason.Key);
        Assert.IsNotEmpty(choice.Reason.Text());
    }

    [TestMethod]
    public void Uplift_is_offered_last_on_every_api_but_d3d8_in_both_bitnesses_and_never_recommended()
    {
        foreach (GraphicsApi api in Enum.GetValues<GraphicsApi>())
        {
            foreach (int bits in new[] { 32, 64 })
            {
                foreach (bool ownDlss in new[] { false, true })
                {
                    RouteChoice choice = Decide(api, bits, ownDlss, gpu: Card(120, "616.92"));

                    bool expected = api != GraphicsApi.DX8;
                    string game = $"{api} {bits}-bit, own DLSS {ownDlss}";
                    Assert.AreEqual(expected, choice.Allows(RouteId.Uplift), game);
                    Assert.AreNotEqual(RouteId.Uplift, choice.Recommended, game);
                    if (expected)
                    {
                        Assert.AreEqual(RouteId.Uplift, choice.Options[^1].Route, game);
                    }
                }
            }
        }
    }

    [TestMethod]
    public void The_recommendation_is_always_offered()
    {
        foreach (GraphicsApi api in Enum.GetValues<GraphicsApi>())
        {
            foreach (Upscaler upscaler in Enum.GetValues<Upscaler>())
            {
                foreach (int? sm in new int?[] { 75, 120, null })
                {
                    RouteChoice choice = Decide(api, 64, ownDlss: upscaler == Upscaler.None, upscaler, gpu: Card(sm, "616.92"));

                    Assert.IsTrue(choice.Recommended is not { } route || choice.Allows(route), $"{api} {upscaler} sm {sm}");
                }
            }
        }
    }

    [TestMethod]
    public void A_remix_game_gets_remix_first_and_recommended_whatever_else_was_decided()
    {
        const string trex = "C:/Games/Portal/.trex";

        RouteChoice choice = Decide(GraphicsApi.DX12, ownDlss: true, remix: trex, gpu: Card(120, "616.92"));

        Assert.AreEqual("remix,native,upstream,optiscaler,bridge,feeder,standalone,renodx,uplift", choice.Ids());
        Assert.AreEqual(RouteId.Remix, choice.Recommended);
        Assert.AreEqual("RouteRemix", choice.Reason.Key);
        Assert.AreEqual(trex, choice.Reason.Joined());
        Assert.AreEqual("RouteNoteRemixOthersRefused", choice.Notes.Keys());
        RouteOption remix = choice.Option(RouteId.Remix);
        Assert.AreEqual("RouteFitRemix", remix.Notes.Keys());
        Assert.AreEqual(RouteReliability.Beta, remix.Reliability);
        Assert.IsEmpty(remix.Warnings, "remix runs no NVIDIA runtime of the driver's");
    }

    [TestMethod]
    public void A_32_bit_d3d9_remix_game_lists_remix_before_the_feeder()
    {
        RouteChoice choice = Decide(GraphicsApi.DX9, 32, remix: "C:/Games/Morrowind/.trex");

        Assert.AreEqual("remix,feeder,uplift", choice.Ids());
        Assert.AreEqual(RouteId.Remix, choice.Recommended);
    }

    [TestMethod]
    public void Only_an_offered_route_may_be_chosen()
    {
        RouteChoice choice = Decide(GraphicsApi.Vulkan, ownDlss: true);

        Assert.IsTrue(choice.Allows(RouteId.Bridge));
        Assert.IsTrue(choice.Allows(RouteId.Uplift));
        Assert.IsFalse(choice.Allows(RouteId.Native));
        Assert.IsFalse(choice.Allows(RouteId.OptiScaler));
    }

    [TestMethod]
    [DataRow(RouteId.Native, "native")]
    [DataRow(RouteId.Upstream, "upstream")]
    [DataRow(RouteId.OptiScaler, "optiscaler")]
    [DataRow(RouteId.Bridge, "bridge")]
    [DataRow(RouteId.Feeder, "feeder")]
    [DataRow(RouteId.Standalone, "standalone")]
    [DataRow(RouteId.Renodx, "renodx")]
    [DataRow(RouteId.Uplift, "uplift")]
    [DataRow(RouteId.Remix, "remix")]
    public void Each_route_has_one_stable_id_both_ways(RouteId route, string id)
    {
        Assert.AreEqual(id, route.Id());
        Assert.IsTrue(RouteIds.TryParse(id, out RouteId parsed));
        Assert.AreEqual(route, parsed);
    }

    [TestMethod]
    [DataRow("Native")]
    [DataRow("neural-upstream")]
    [DataRow("")]
    [DataRow(null)]
    public void Anything_else_is_not_a_route_id(string? id) => Assert.IsFalse(RouteIds.TryParse(id, out _));

    [TestMethod]
    public void A_value_outside_the_nine_routes_has_no_id() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ((RouteId)42).Id());
}
