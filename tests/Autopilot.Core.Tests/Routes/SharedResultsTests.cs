using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using static Autopilot.Core.Tests.Routes.RouteGames;

namespace Autopilot.Core.Tests.Routes;

/// <summary>The shared results move the recommendation: this game's own first, else its class when the ranges do not overlap.</summary>
[TestClass]
public sealed class SharedResultsTests
{
    private const string Source = "compatibility.json";

    private static readonly DateTimeOffset _date = new(2026, 11, 20, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeLogger _log = new();

    private static SharedResults List(Dictionary<string, RouteTally>? game = null, Dictionary<string, RouteTally>? klass = null) =>
        new(Source, _date) { Game = game ?? [], Class = klass ?? [] };

    // A D3D11 game with nothing of its own: feeder recommended, bridge, standalone, renodx and uplift offered.
    private RouteChoice Plain(SharedResults shared) => Decide(GraphicsApi.DX11, shared: shared, logger: _log);

    [TestMethod]
    public void Enough_results_for_this_game_move_it_to_the_route_that_worked_there()
    {
        RouteChoice choice = Plain(List(game: new() { ["bridge"] = new(4, 1), ["feeder"] = new(0, 3) }));

        Assert.AreEqual(RouteId.Bridge, choice.Recommended);
        Assert.AreEqual(new SharedMove(SharedScope.Game, RouteId.Feeder, Source, _date), choice.MovedBy);
        Assert.AreEqual("RouteSharedGame", choice.Reason.Key);
        Assert.AreEqual($"bridge|4|5|feeder|{Source}|2026-11-20", choice.Reason.Joined());
        Assert.Contains("4 of 5", choice.Reason.Text());
    }

    [TestMethod]
    public void This_games_results_rank_by_the_surer_rate_then_by_tries()
    {
        // 2 of 2 is a higher rate than 9 of 12 and a worse bet.
        RouteChoice choice = Plain(List(game: new() { ["standalone"] = new(2, 0), ["renodx"] = new(9, 3) }));

        Assert.AreEqual(RouteId.Renodx, choice.Recommended);
    }

    [TestMethod]
    public void Equal_rates_for_this_game_go_to_the_more_tried_route()
    {
        RouteChoice choice = Plain(List(game: new() { ["bridge"] = new(3, 0), ["standalone"] = new(6, 0) }));

        Assert.AreEqual(RouteId.Standalone, choice.Recommended);
    }

    [TestMethod]
    public void When_this_games_best_is_the_recommendation_its_class_is_not_asked()
    {
        RouteChoice choice = Plain(List(
            game: new() { ["feeder"] = new(5, 0) },
            klass: new() { ["feeder"] = new(5, 25), ["bridge"] = new(29, 1) }));

        Assert.AreEqual(RouteId.Feeder, choice.Recommended);
        Assert.IsNull(choice.MovedBy);
        Assert.AreEqual("RouteNoDlss", choice.Reason.Key);
    }

    [TestMethod]
    public void This_games_results_that_never_worked_move_nothing_and_its_class_is_not_asked()
    {
        RouteChoice choice = Plain(List(
            game: new() { ["feeder"] = new(0, 3), ["bridge"] = new(0, 3) },
            klass: new() { ["feeder"] = new(5, 25), ["bridge"] = new(29, 1) }));

        Assert.AreEqual(RouteId.Feeder, choice.Recommended);
        Assert.IsNull(choice.MovedBy);
    }

    [TestMethod]
    public void Too_few_results_for_this_game_leave_it_to_the_class()
    {
        RouteChoice choice = Plain(List(
            game: new() { ["bridge"] = new(4, 0) },
            klass: new() { ["feeder"] = new(10, 20), ["bridge"] = new(28, 2) }));

        Assert.AreEqual(RouteId.Bridge, choice.Recommended);
        Assert.AreEqual(SharedScope.Class, choice.MovedBy!.Scope);
        Assert.AreEqual(RouteId.Feeder, choice.MovedBy.From);
        Assert.AreEqual("RouteSharedClass", choice.Reason.Key);
        Assert.AreEqual($"bridge|28|30|feeder|10|30|{Source}|2026-11-20", choice.Reason.Joined());
        Assert.Contains("10 of 30", choice.Reason.Text());
    }

    [TestMethod]
    public void Class_ranges_that_overlap_move_nothing()
    {
        RouteChoice choice = Plain(List(klass: new() { ["feeder"] = new(15, 10), ["bridge"] = new(19, 6) }));

        Assert.AreEqual(RouteId.Feeder, choice.Recommended);
        Assert.IsNull(choice.MovedBy);
    }

    [TestMethod]
    [DataRow(19, 20)]
    [DataRow(20, 19)]
    public void A_class_needs_twenty_results_on_both_routes(int feederTries, int bridgeTries)
    {
        RouteChoice choice = Plain(List(klass: new() { ["feeder"] = new(0, feederTries), ["bridge"] = new(bridgeTries, 0) }));

        Assert.AreEqual(RouteId.Feeder, choice.Recommended);
    }

    [TestMethod]
    public void The_class_picks_the_surest_of_several_clear_winners()
    {
        RouteChoice choice = Plain(List(klass: new()
        {
            ["feeder"] = new(2, 28),
            ["bridge"] = new(20, 2),
            ["standalone"] = new(40, 1),
        }));

        Assert.AreEqual(RouteId.Standalone, choice.Recommended);
    }

    [TestMethod]
    public void Uplift_is_never_the_pick_even_as_the_best()
    {
        Assert.AreEqual(RouteId.Feeder, Plain(List(game: new() { ["uplift"] = new(9, 0) })).Recommended);
        Assert.AreEqual(RouteId.Feeder, Plain(List(klass: new() { ["feeder"] = new(2, 28), ["uplift"] = new(30, 0) })).Recommended);
    }

    [TestMethod]
    public void A_route_not_on_offer_is_never_the_pick()
    {
        Assert.AreEqual(RouteId.Feeder, Plain(List(game: new() { ["native"] = new(9, 0) })).Recommended);
        Assert.AreEqual(RouteId.Feeder, Plain(List(klass: new() { ["feeder"] = new(2, 28), ["native"] = new(30, 0) })).Recommended);
        Assert.IsNull(Plain(List(klass: new() { ["feeder"] = new(2, 28), ["native"] = new(30, 0) })).MovedBy);
    }

    [TestMethod]
    public void A_remix_game_is_never_moved()
    {
        RouteChoice choice = Decide(GraphicsApi.DX9, 32, remix: "C:/Games/Portal/.trex", shared: List(game: new() { ["feeder"] = new(9, 0) }), logger: _log);

        Assert.AreEqual(RouteId.Remix, choice.Recommended);
        Assert.IsNull(choice.MovedBy);
    }

    [TestMethod]
    public void A_game_whose_api_is_asked_gets_no_recommendation_from_the_list()
    {
        RouteChoice choice = Decide(GraphicsApi.Dxgi, ownDlss: true, shared: List(game: new() { ["bridge"] = new(9, 0) }), logger: _log);

        Assert.IsNull(choice.Recommended);
        Assert.IsNull(choice.MovedBy);
    }

    [TestMethod]
    [DataRow("feeder", -1, 5, "negative count")]
    [DataRow("teleport", 9, 0, "'teleport'")]
    public void A_malformed_list_changes_nothing_and_is_logged(string route, int worked, int failed, string problem)
    {
        RouteChoice choice = Plain(List(game: new() { ["bridge"] = new(9, 0), [route] = new(worked, failed) }));

        Assert.AreEqual(RouteId.Feeder, choice.Recommended);
        Assert.IsNull(choice.MovedBy);
        Assert.AreEqual(LogLevel.Warning, _log.LatestRecord.Level);
        Assert.Contains(Source, _log.LatestRecord.Message);
        Assert.Contains(problem, _log.LatestRecord.Message);
    }

    [TestMethod]
    public void A_malformed_class_table_is_logged_too()
    {
        RouteChoice choice = Plain(List(klass: new() { ["feeder"] = new(2, 28), ["bridge"] = new(30, -1) }));

        Assert.AreEqual(RouteId.Feeder, choice.Recommended);
        Assert.Contains("class", _log.LatestRecord.Message);
    }

    [TestMethod]
    public void No_list_logs_nothing()
    {
        Assert.AreEqual(RouteId.Feeder, Decide(GraphicsApi.DX11, logger: _log).Recommended);
        Assert.AreEqual(0, _log.Collector.Count);
    }

    [TestMethod]
    [DataRow(2, 2, 0.3424)]
    [DataRow(9, 12, 0.4677)]
    [DataRow(0, 0, 0.0)]
    public void The_lower_bound_is_wilsons_at_95_percent(int worked, int tried, double lower) =>
        Assert.AreEqual(lower, SharedSteer.Lower(new RouteTally(worked, tried - worked)), 0.0005);

    [TestMethod]
    public void The_upper_bound_mirrors_the_lower_one()
    {
        Assert.AreEqual(1 - 0.3424, SharedSteer.Upper(new RouteTally(0, 2)), 0.0005);
        Assert.AreEqual(1.0, SharedSteer.Upper(new RouteTally(0, 0)), 1e-9);
    }
}
