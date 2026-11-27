namespace Autopilot.Core.Routes;

/// <summary>The route list and the recommendation while the rules and the steps after them shape it.</summary>
internal sealed record RouteDraft(IReadOnlyList<RouteId> Options, RouteId? Recommended, RouteMessage Reason)
{
    public IReadOnlyList<RouteMessage> Notes { get; init; } = [];

    public RouteQuestion Asks { get; init; }

    public SharedMove? MovedBy { get; init; }
}
