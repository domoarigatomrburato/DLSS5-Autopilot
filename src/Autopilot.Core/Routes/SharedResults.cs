namespace Autopilot.Core.Routes;

/// <summary>How many shared results say a route worked and how many say it failed.</summary>
/// <param name="Worked">Results that say it worked.</param>
/// <param name="Failed">Results that say it did not.</param>
public readonly record struct RouteTally(int Worked, int Failed);

/// <summary>
/// The community's shared results that can move a game's recommendation: the counts for this game
/// and for its class (the API, and whether it ships DLSS of its own, FSR, XeSS or nothing), keyed
/// by route id. A malformed list (a negative count, an id that names no route) changes nothing and
/// is logged.
/// </summary>
/// <param name="Source">Where the list came from, as the preview names it.</param>
/// <param name="Date">When the list was published, as the preview names it.</param>
public sealed record SharedResults(string Source, DateTimeOffset Date)
{
    /// <summary>This game's results per route id.</summary>
    public IReadOnlyDictionary<string, RouteTally> Game { get; init; } = new Dictionary<string, RouteTally>();

    /// <summary>The results of the game's class per route id.</summary>
    public IReadOnlyDictionary<string, RouteTally> Class { get; init; } = new Dictionary<string, RouteTally>();
}
