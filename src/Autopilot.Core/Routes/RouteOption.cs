namespace Autopilot.Core.Routes;

/// <summary>One entry of a game's route dropdown.</summary>
/// <param name="Route">The route.</param>
/// <param name="Usable">Whether it can work for this game on this card; <paramref name="Notes"/> says why not.</param>
/// <param name="Notes">The short note the dropdown shows beside it ("fit"), in order.</param>
/// <param name="Reliability">How likely it is to work, for this game's API and architecture.</param>
/// <param name="Warnings">What the driver means for this route, and OptiScaler's card-and-driver note; empty when nothing applies.</param>
public sealed record RouteOption(
    RouteId Route,
    bool Usable,
    IReadOnlyList<RouteMessage> Notes,
    RouteReliability Reliability,
    IReadOnlyList<RouteMessage> Warnings);
