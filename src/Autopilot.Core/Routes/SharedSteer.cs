using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Routes;

/// <summary>
/// Step 5: the community's shared results move the recommendation, never to a route that is not
/// offered, is uplift, or is not usable here. Enough results for this game decide alone; otherwise
/// the game's class does, when two routes have enough results and their ranges do not overlap.
/// </summary>
internal static partial class SharedSteer
{
    /// <summary>Results for this game before they mean anything: below it, two people are not a finding.</summary>
    public const int MinReports = 5;

    /// <summary>Results a route needs in the game's class, on both routes compared, before the class moves anything.</summary>
    public const int MinClass = 20;

    // 95% two-sided.
    private const double Z = 1.96;

    /// <summary>The draft, moved when the shared results say so clearly; as it was otherwise, or when the list is malformed.</summary>
    public static RouteDraft Apply(RouteDraft draft, SharedResults? shared, Func<RouteId, bool> usable, ILogger logger)
    {
        if (shared is null || draft.Recommended is not { } current || current == RouteId.Remix)
        {
            return draft;
        }

        if (Problem(shared) is { } problem)
        {
            LogMalformed(logger, shared.Source, shared.Date, problem);
            return draft;
        }

        Dictionary<RouteId, RouteTally> game = Parse(shared.Game);
        RouteId[] candidates = [.. draft.Options.Where(route => route != RouteId.Uplift && usable(route))];
        if (game.Values.Sum(Tries) >= MinReports)
        {
            RouteId? best = GamePick(game, candidates);
            return best is { } pick && pick != current ? Move(draft, SharedScope.Game, pick, game, shared) : draft;
        }

        Dictionary<RouteId, RouteTally> klass = Parse(shared.Class);
        return ClassPick(klass, candidates, current) is { } chosen ? Move(draft, SharedScope.Class, chosen, klass, shared) : draft;
    }

    /// <summary>The lower end of the 95% Wilson interval: how much a rate is worth, given how few results are behind it.</summary>
    internal static double Lower(RouteTally tally)
    {
        long tries = Tries(tally);
        if (tries <= 0)
        {
            return 0;
        }

        double n = tries;
        double rate = tally.Worked / n;
        double centre = rate + (Z * Z / (2 * n));
        double spread = Z * Math.Sqrt((rate * (1 - rate) / n) + (Z * Z / (4 * n * n)));
        return Math.Max(0, (centre - spread) / (1 + (Z * Z / n)));
    }

    /// <summary>The upper end of the same interval: the best the rate could be.</summary>
    internal static double Upper(RouteTally tally) => 1 - Lower(new RouteTally(tally.Failed, tally.Worked));

    // The route that worked for this game, best first by its lower bound, then by how often it was tried, then in dropdown order.
    private static RouteId? GamePick(Dictionary<RouteId, RouteTally> game, RouteId[] candidates) =>
        candidates
            .Where(route => game.GetValueOrDefault(route).Worked > 0)
            .OrderByDescending(route => Lower(game[route]))
            .ThenByDescending(route => Tries(game[route]))
            .Cast<RouteId?>()
            .FirstOrDefault();

    // Another route whose lowest likely rate clears the current one's highest, both with enough results; the surest if several do.
    private static RouteId? ClassPick(Dictionary<RouteId, RouteTally> klass, RouteId[] candidates, RouteId current)
    {
        if (!klass.TryGetValue(current, out RouteTally now) || Tries(now) < MinClass)
        {
            return null;
        }

        return candidates
            .Where(route => route != current && klass.TryGetValue(route, out RouteTally other) && Tries(other) >= MinClass && Lower(other) > Upper(now))
            .OrderByDescending(route => Lower(klass[route]))
            .Cast<RouteId?>()
            .FirstOrDefault();
    }

    private static RouteDraft Move(RouteDraft draft, SharedScope scope, RouteId pick, Dictionary<RouteId, RouteTally> counts, SharedResults shared)
    {
        RouteId from = draft.Recommended!.Value;
        RouteTally picked = counts[pick];
        string date = shared.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        RouteMessage reason = scope == SharedScope.Game
            ? new RouteMessage("RouteSharedGame", pick.Name(), Count(picked.Worked), Count(Tries(picked)), from.Name(), shared.Source, date)
            : new RouteMessage(
                "RouteSharedClass",
                pick.Name(),
                Count(picked.Worked),
                Count(Tries(picked)),
                from.Name(),
                Count(counts[from].Worked),
                Count(Tries(counts[from])),
                shared.Source,
                date);
        return draft with { Recommended = pick, Reason = reason, MovedBy = new SharedMove(scope, from, shared.Source, shared.Date) };
    }

    // What makes the list unusable, in words for the log; null when nothing does.
    private static string? Problem(SharedResults shared)
    {
        foreach ((string table, IReadOnlyDictionary<string, RouteTally> counts) in new[] { ("game", shared.Game), ("class", shared.Class) })
        {
            foreach ((string id, RouteTally tally) in counts)
            {
                if (!RouteIds.TryParse(id, out _))
                {
                    return $"the {table} results name a route '{id}' that does not exist";
                }

                if (tally.Worked < 0 || tally.Failed < 0)
                {
                    return $"the {table} results for '{id}' carry a negative count";
                }
            }
        }

        return null;
    }

    private static Dictionary<RouteId, RouteTally> Parse(IReadOnlyDictionary<string, RouteTally> counts) =>
        counts.ToDictionary(
            pair =>
            {
                _ = RouteIds.TryParse(pair.Key, out RouteId route);
                return route;
            },
            pair => pair.Value);

    private static long Tries(RouteTally tally) => (long)tally.Worked + tally.Failed;

    private static string Count(long count) => count.ToString(CultureInfo.InvariantCulture);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The shared results from {Source} of {Date} were left out of the route recommendation: {Problem}")]
    private static partial void LogMalformed(ILogger logger, string source, DateTimeOffset date, string problem);
}
