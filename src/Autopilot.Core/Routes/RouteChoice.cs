using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>What the person has to settle before a route can be recommended.</summary>
public enum RouteQuestion
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>The graphics API: D3D11 and D3D12 are not settled, or nothing was read.</summary>
    Api,

    /// <summary>The architecture: it could not be read, and no route is offered until it is chosen.</summary>
    Architecture,
}

/// <summary>Whose shared results moved a recommendation.</summary>
public enum SharedScope
{
    /// <summary>Results for this game.</summary>
    Game,

    /// <summary>Results for games of its class, with ranges that do not overlap.</summary>
    Class,
}

/// <summary>A recommendation the shared results moved.</summary>
/// <param name="Scope">Whose results moved it.</param>
/// <param name="From">The route the tool would have picked without them.</param>
/// <param name="Source">The list's source.</param>
/// <param name="Date">The list's date.</param>
public sealed record SharedMove(SharedScope Scope, RouteId From, string Source, DateTimeOffset Date);

/// <summary>
/// The routes a game is offered and the one recommended, with the reason: the one place the route
/// list is decided. The install plan and the app read it and never derive it again.
/// </summary>
public sealed record RouteChoice
{
    /// <summary>The offered routes in dropdown order; empty when the architecture must be chosen first or the game cannot be served.</summary>
    public required IReadOnlyList<RouteOption> Options { get; init; }

    /// <summary>The recommended route, always one of <see cref="Options"/>; null when the person is asked (<see cref="Asks"/>) or nothing is offered.</summary>
    public RouteId? Recommended { get; init; }

    /// <summary>Why this recommendation, or why none.</summary>
    public required RouteMessage Reason { get; init; }

    /// <summary>What else the person should know about the game's routes, after the reason.</summary>
    public IReadOnlyList<RouteMessage> Notes { get; init; } = [];

    /// <summary>What the person is asked before a route is recommended.</summary>
    public RouteQuestion Asks { get; init; }

    /// <summary>When the API is asked, what the executable said about it (evidence and confidence); null otherwise or when it was not read.</summary>
    public ApiDecision? ApiEvidence { get; init; }

    /// <summary>The shared results that moved the recommendation, with the list's source and date; null when none did.</summary>
    public SharedMove? MovedBy { get; init; }

    /// <summary>The recommended entry of <see cref="Options"/>, with its notes and warnings; null when nothing is recommended.</summary>
    public RouteOption? RecommendedOption => Options.FirstOrDefault(option => option.Route == Recommended);

    /// <summary>Whether the person may use <paramref name="route"/> for this game: only an offered route can be chosen.</summary>
    public bool Allows(RouteId route) => Options.Any(option => option.Route == route);
}
